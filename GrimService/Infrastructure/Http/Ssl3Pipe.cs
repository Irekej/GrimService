using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using BcCertificate = Org.BouncyCastle.Tls.Certificate;

namespace GrimService.Infrastructure.Http
{


    internal sealed class Ssl3Pipe : IDuplexPipe, IAsyncDisposable
    {
        private readonly IDuplexPipe network;
        private readonly Pipe decrypted = new();
        private readonly Pipe plaintext = new();
        private readonly DiagnosticProtocol protocol;
        private readonly SemaphoreSlim protocolLock = new(1);
        private readonly CancellationTokenSource stop;
        private readonly TaskCompletionSource<int> handshake = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly byte[] encryptedBuffer = new byte[16384];
        private readonly Task receive;
        private readonly Task send;
        private readonly Action<string> trace;
        private long receivedBytes;
        private long sentBytes;
        private int peerInputCompleted;
        private bool firstFlightFlushed;

        public Ssl3Pipe(IDuplexPipe network, Ssl3Credentials credentials,
            Action<string> offeredCipherSuites = null, Action<string> trace = null)
        {
            this.network = network;
            this.trace = trace;



            stop = new CancellationTokenSource();
            protocol = new DiagnosticProtocol(trace);
            protocol.Accept(new Server(credentials, cipher => handshake.TrySetResult(cipher), offeredCipherSuites, trace));
            receive = ReceiveAsync();
            send = SendAsync();
        }

        public PipeReader Input => decrypted.Reader;
        public PipeWriter Output => plaintext.Writer;
        public Task<int> Handshake => handshake.Task;
        public Exception Error { get; private set; }
        public string HandshakeState => protocol.State;
        public long ReceivedBytes => Interlocked.Read(ref receivedBytes);
        public long SentBytes => Interlocked.Read(ref sentBytes);
        public bool PeerInputCompleted => Volatile.Read(ref peerInputCompleted) != 0;

        private async Task ReceiveAsync()
        {
            var buffer = new byte[16384];
            try
            {
                while (true)
                {
                    var result = await network.Input.ReadAsync(stop.Token);
                    if (result.IsCompleted) Volatile.Write(ref peerInputCompleted, 1);
                    Interlocked.Add(ref receivedBytes, result.Buffer.Length);
                    try
                    {
                        await protocolLock.WaitAsync(stop.Token);
                        try
                        {
                            foreach (var part in result.Buffer)
                            {
                                protocol.OfferInput(part.ToArray());
                                while (protocol.GetAvailableInputBytes() > 0)
                                {
                                    int count = protocol.ReadInput(buffer, 0, buffer.Length);
                                    Copy(decrypted.Writer, buffer, count);
                                }
                            }
                            await FlushEncryptedAsync();
                        }
                        finally { protocolLock.Release(); }
                    }
                    finally { network.Input.AdvanceTo(result.Buffer.End); }
                    if ((await decrypted.Writer.FlushAsync(stop.Token)).IsCompleted || result.IsCompleted || result.IsCanceled)
                        break;
                }
            }
            catch (Exception error) { Fail(error); }
            finally
            {
                Array.Clear(buffer);
                handshake.TrySetException(new IOException("Connection ended before the SSL 3.0 handshake completed."));
                await decrypted.Writer.CompleteAsync(Error);
            }
        }

        private async Task SendAsync()
        {
            try
            {
                while (true)
                {
                    var result = await plaintext.Reader.ReadAsync(stop.Token);
                    try
                    {
                        await protocolLock.WaitAsync(stop.Token);
                        try
                        {
                            foreach (var part in result.Buffer)
                            {
                                byte[] bytes = part.ToArray();
                                try { protocol.WriteApplicationData(bytes, 0, bytes.Length); }
                                finally { Array.Clear(bytes); }
                            }
                            if (result.IsCompleted) protocol.Close();
                            await FlushEncryptedAsync();
                        }
                        finally { protocolLock.Release(); }
                    }
                    finally { plaintext.Reader.AdvanceTo(result.Buffer.End); }
                    if (result.IsCompleted || result.IsCanceled) break;
                }
            }
            catch (Exception error) { Fail(error); }
            finally { await plaintext.Reader.CompleteAsync(Error); }
        }

        private async Task FlushEncryptedAsync()
        {
            int flushed = 0;
            while (protocol.GetAvailableOutputBytes() > 0)
            {
                int count = protocol.ReadOutput(encryptedBuffer, 0, encryptedBuffer.Length);
                Copy(network.Output, encryptedBuffer, count);
                flushed += count;
            }
            var result = await network.Output.FlushAsync(stop.Token);
            if (result.IsCanceled || result.IsCompleted) throw new IOException("SSL 3.0 network output closed.");
            Interlocked.Add(ref sentBytes, flushed);
            if (flushed > 0 && !firstFlightFlushed)
            {
                firstFlightFlushed = true;
                trace?.Invoke($"first-server-flight-flushed, bytes={flushed}, state={protocol.State}");
            }
        }

        private static void Copy(PipeWriter writer, byte[] bytes, int count)
        {
            bytes.AsSpan(0, count).CopyTo(writer.GetSpan(count));
            writer.Advance(count);
        }

        private void Fail(Exception error)
        {
            if (!stop.IsCancellationRequested) Error = error;
            handshake.TrySetException(error);
            stop.Cancel();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await plaintext.Writer.CompleteAsync();

                await send.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException) { }
            finally
            {
                stop.Cancel();
                await Task.WhenAll(receive, send);
                await decrypted.Reader.CompleteAsync();
                stop.Dispose();
                protocolLock.Dispose();
            }
        }

        private sealed class DiagnosticProtocol : TlsServerProtocol
        {
            private readonly Action<string> trace;
            public DiagnosticProtocol(Action<string> trace) { this.trace = trace; }
            public string State => $"{StateName}, clientCcs={m_receivedChangeCipherSpec}, closed={IsClosed}";
            private string StateName => m_connectionState switch {
                CS_START => "waiting-client-hello",
                CS_CLIENT_HELLO => "received-client-hello",
                CS_SERVER_HELLO => "sent-server-hello",
                CS_SERVER_CERTIFICATE => "sent-server-certificate",
                CS_SERVER_HELLO_DONE => "waiting-client-key-exchange",
                CS_CLIENT_KEY_EXCHANGE => "waiting-client-finished",
                CS_CLIENT_FINISHED => "received-client-finished",
                CS_SERVER_FINISHED => "sent-server-finished",
                CS_END => "established",
                _ => $"state-{m_connectionState}"
            };
            protected override void HandleHandshakeMessage(short type, HandshakeMessageInput message)
            {
                trace?.Invoke($"received-{HandshakeType.GetText(type)}, bytes={message.Length}");
                base.HandleHandshakeMessage(type, message);
            }
            protected override void SendServerHelloMessage(ServerHello hello)
            {
                base.SendServerHelloMessage(hello);
                trace?.Invoke($"queued-server-hello, version={hello.Version}, cipher=0x{hello.CipherSuite:x4}, sessionBytes={hello.SessionID.Length}, extensions={hello.Extensions?.Count ?? 0}");
            }
            protected override void SendCertificateMessage(BcCertificate certificate, System.IO.Stream endPointHash)
            {
                base.SendCertificateMessage(certificate, endPointHash);
                trace?.Invoke($"queued-server-certificate, chainLength={certificate.Length}");
            }
            protected override void SendServerHelloDoneMessage()
            {
                base.SendServerHelloDoneMessage();
                trace?.Invoke("queued-server-hello-done");
            }
            protected override void HandleChangeCipherSpecMessage()
            {
                trace?.Invoke("received-client-change-cipher-spec");
                base.HandleChangeCipherSpecMessage();
            }
        }

        private sealed class Server : DefaultTlsServer
        {
            private readonly TlsCredentialedDecryptor decryptor;
            private readonly Action<int> ready;
            private readonly Action<string> offeredCipherSuites;
            private readonly Action<string> trace;
            public Server(Ssl3Credentials credentials, Action<int> ready, Action<string> offeredCipherSuites,
                Action<string> trace) : base(new Ssl3Crypto())
            {
                this.ready = ready;
                this.offeredCipherSuites = offeredCipherSuites;
                this.trace = trace;
                var crypto = (BcTlsCrypto)Crypto;
                decryptor = new BcDefaultTlsCredentialedDecryptor(crypto,
                    new BcCertificate(new[] { crypto.CreateCertificate(credentials.Certificate) }), credentials.Key);
            }
            protected override ProtocolVersion[] GetSupportedVersions() => new[] { ProtocolVersion.SSLv3 };
            protected override int[] GetSupportedCipherSuites() => new[] {
                CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA, CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA,
                CipherSuite.TLS_RSA_WITH_3DES_EDE_CBC_SHA,
                CipherSuite.TLS_RSA_WITH_RC4_128_SHA, CipherSuite.TLS_RSA_WITH_RC4_128_MD5 };
            protected override bool PreferLocalCipherSuites() => true;
            protected override TlsCredentialedDecryptor GetRsaEncryptionCredentials() => decryptor;
            public override void NotifyOfferedCipherSuites(int[] suites)
            {
                base.NotifyOfferedCipherSuites(suites);
                offeredCipherSuites?.Invoke(DescribeSuites(suites));
            }
            protected override string GetDetailMessageNoCipherSuite() =>
                $"No selectable cipher suite; client=[{DescribeSuites(m_offeredCipherSuites)}]; server=[{DescribeSuites(GetCipherSuites())}]";
            private static string DescribeSuites(int[] suites) => suites == null ? "unavailable" :
                string.Join(", ", suites.Take(64).Select(suite => $"0x{suite:x4} ({Ssl3ConnectionFeature.GetCipherName(suite)})")) +
                (suites.Length > 64 ? $", ... ({suites.Length} total)" : "");

            public override void NotifySecureRenegotiation(bool secureRenegotiation) { }
            public override void NotifyAlertReceived(short level, short description)
            {
                trace?.Invoke($"peer-alert, level={AlertLevel.GetText(level)}, description={AlertDescription.GetText(description)}");
                base.NotifyAlertReceived(level, description);
            }
            public override void NotifyAlertRaised(short level, short description, string message, Exception cause)
            {
                trace?.Invoke($"server-alert, level={AlertLevel.GetText(level)}, description={AlertDescription.GetText(description)}");
                base.NotifyAlertRaised(level, description, message, cause);
            }
            public override void NotifyHandshakeComplete()
            {
                base.NotifyHandshakeComplete();
                trace?.Invoke("handshake-complete");
                ready(m_context.SecurityParameters.CipherSuite);
            }
        }
    }
}
