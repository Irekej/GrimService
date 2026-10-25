using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Tls;

namespace GrimService.Infrastructure.Http
{


    internal sealed class Ssl3ConnectionMiddleware
    {
        private readonly ConnectionDelegate next;
        private readonly ILogger logger;
        private readonly Ssl3Credentials credentials;
        private readonly SemaphoreSlim connections = new(256);

        public Ssl3ConnectionMiddleware(ConnectionDelegate next, X509Certificate2 certificate, ILogger logger)
        {
            this.next = next;
            this.logger = logger;
            using var rsa = certificate.GetRSAPublicKey() ??
                throw new InvalidOperationException("GT5 SSL 3.0 requires a server certificate with an RSA private key.");


            byte[] encoded = certificate.Export(X509ContentType.Pfx);
            try
            {
                var store = new Pkcs12StoreBuilder().Build();
                store.Load(new MemoryStream(encoded, writable: false), Array.Empty<char>());
                foreach (string alias in store.Aliases)
                {
                    if (!store.IsKeyEntry(alias)) continue;
                    if (!store.GetCertificate(alias).Certificate.GetEncoded().AsSpan().SequenceEqual(certificate.RawData)) continue;
                    if (store.GetKey(alias).Key is not RsaKeyParameters key || !key.IsPrivate) continue;
                    credentials = new Ssl3Credentials(certificate.RawData, key, rsa.KeySize);
                    break;
                }
                if (credentials == null) throw new InvalidOperationException("RSA private key is missing from the GT5 server certificate.");
            }
            finally { CryptographicOperations.ZeroMemory(encoded); }
        }

        public async Task OnConnectionAsync(ConnectionContext connection)
        {
            var elapsed = Stopwatch.StartNew();
            using var timeout = new CancellationTokenSource();
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            bool ssl3;
            try { ssl3 = await IsSsl3Hello(connection.Transport.Input, timeout.Token); }
            catch (OperationCanceledException) { connection.Abort(); return; }
            if (!ssl3) { await next(connection); return; }
            if (!await connections.WaitAsync(0)) { connection.Abort(); return; }

            var original = connection.Transport;
            var originalTls = connection.Features.Get<ITlsConnectionFeature>();
            var originalHandshake = connection.Features.Get<ITlsHandshakeFeature>();
            var originalAlpn = connection.Features.Get<ITlsApplicationProtocolFeature>();
            Ssl3Pipe transport = null;
            try
            {
                await using var pipe = transport = new Ssl3Pipe(original, credentials,
                    suites => logger.LogDebug("GT5 SSL 3.0 ClientHello: connection={Connection}, remote={Remote}, offeredCiphers={OfferedCiphers}",
                        connection.ConnectionId, connection.RemoteEndPoint, suites),
                    stage => logger.LogDebug("GT5 SSL 3.0 handshake event: connection={Connection}, elapsedMs={ElapsedMs}, event={HandshakeEvent}",
                        connection.ConnectionId, elapsed.ElapsedMilliseconds, stage));
                int cipher = await pipe.Handshake.WaitAsync(timeout.Token);
                var feature = new Ssl3ConnectionFeature(cipher, credentials.KeyBits);


                connection.Features.Set<ITlsConnectionFeature>(feature);
                connection.Features.Set<ITlsHandshakeFeature>(feature);
                connection.Features.Set<ITlsApplicationProtocolFeature>(feature);
                connection.Transport = pipe;
                logger.LogInformation("GT5 SSL 3.0 handshake accepted: connection={Connection}, remote={Remote}, cipher={Cipher}",
                    connection.ConnectionId, connection.RemoteEndPoint, feature.CipherName);
                await next(connection);
                if (pipe.Error != null)
                    logger.LogDebug(pipe.Error, "GT5 SSL 3.0 transport closed: connection={Connection}", connection.ConnectionId);
            }
            catch (Exception error) when (error is IOException || error is OperationCanceledException)
            {
                string reason = error is TlsFatalAlertReceived ? "peer-alert" :
                    timeout.IsCancellationRequested ? "handshake-timeout" :
                    transport?.PeerInputCompleted == true ? "peer-eof" :
                    connection.ConnectionClosed.IsCancellationRequested ? "connection-closed" : "protocol-or-transport";
                logger.LogDebug(error, "GT5 SSL 3.0 handshake/connection failed: connection={Connection}, remote={Remote}, reason={Reason}, elapsedMs={ElapsedMs}, state={HandshakeState}, receivedBytes={ReceivedBytes}, sentBytes={SentBytes}",
                    connection.ConnectionId, connection.RemoteEndPoint, reason, elapsed.ElapsedMilliseconds,
                    transport?.HandshakeState, transport?.ReceivedBytes, transport?.SentBytes);
                connection.Abort();
            }
            finally
            {
                connection.Transport = original;
                connection.Features.Set(originalTls);
                connection.Features.Set(originalHandshake);
                connection.Features.Set(originalAlpn);
                connections.Release();
            }
        }

        private static async Task<bool> IsSsl3Hello(PipeReader input, CancellationToken cancellation)
        {
            SequencePosition examined = default;
            bool haveExamined = false;
            while (true)
            {
                var result = await input.ReadAsync(cancellation);
                var buffer = result.Buffer;
                if (buffer.Length >= 11)
                {
                    bool ssl3 = HasSsl3Version(buffer);


                    input.AdvanceTo(buffer.Start, haveExamined ? examined : buffer.Start);
                    return ssl3;
                }
                if (result.IsCompleted || result.IsCanceled)
                {
                    input.AdvanceTo(buffer.Start, haveExamined ? examined : buffer.Start);
                    return false;
                }
                examined = buffer.End;
                haveExamined = true;
                input.AdvanceTo(buffer.Start, examined);
            }
        }

        private static bool HasSsl3Version(ReadOnlySequence<byte> buffer)
        {
            Span<byte> header = stackalloc byte[11];
            buffer.Slice(0, 11).CopyTo(header);
            return header[0] == 22 && header[1] == 3 && header[5] == 1 && header[9] == 3 && header[10] == 0;
        }
    }

    internal sealed record Ssl3Credentials(byte[] Certificate, AsymmetricKeyParameter Key, int KeyBits);

    internal sealed class Ssl3ConnectionFeature : ITlsConnectionFeature, ITlsHandshakeFeature, ITlsApplicationProtocolFeature
    {
        private readonly int cipher;
        public Ssl3ConnectionFeature(int cipher, int keyBits) { this.cipher = cipher; KeyExchangeStrength = keyBits; }
        public string CipherName => GetCipherName(cipher);
        public static string GetCipherName(int cipher) => cipher switch {
            CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA => "RSA-AES128-CBC-SHA",
            CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA => "RSA-AES256-CBC-SHA",
            CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA => "RSA-3DES-EDE-CBC-SHA",
            CipherSuite.TLS_RSA_WITH_RC4_128_SHA => "RSA-RC4-128-SHA",
            CipherSuite.TLS_RSA_WITH_RC4_128_MD5 => "RSA-RC4-128-MD5",
            _ => "unknown"
        };
        public X509Certificate2 ClientCertificate { get; set; }
        public Task<X509Certificate2> GetClientCertificateAsync(CancellationToken cancellationToken) => Task.FromResult(ClientCertificate);
#pragma warning disable CS0618 // SSL3 is implemented by BouncyCastle, not SslStream.
        public SslProtocols Protocol => SslProtocols.Ssl3;
#pragma warning restore CS0618
        public CipherAlgorithmType CipherAlgorithm => cipher switch {
            CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA => CipherAlgorithmType.Aes128,
            CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA => CipherAlgorithmType.Aes256,
            CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA => CipherAlgorithmType.TripleDes,
            _ => CipherAlgorithmType.Rc4
        };
        public int CipherStrength => cipher switch {
            CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA => 128,
            CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA => 256,
            CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA => 168,
            _ => 128
        };
        public HashAlgorithmType HashAlgorithm => cipher == CipherSuite.TLS_RSA_WITH_RC4_128_MD5 ? HashAlgorithmType.Md5 : HashAlgorithmType.Sha1;
        public int HashStrength => cipher == CipherSuite.TLS_RSA_WITH_RC4_128_MD5 ? 128 : 160;
        public ExchangeAlgorithmType KeyExchangeAlgorithm => ExchangeAlgorithmType.RsaKeyX;
        public int KeyExchangeStrength { get; }
        public string HostName => string.Empty;
        public ReadOnlyMemory<byte> ApplicationProtocol => ReadOnlyMemory<byte>.Empty;
    }
}
