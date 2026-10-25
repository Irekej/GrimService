using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using GrimService.Config;
using Microsoft.Extensions.Options;

namespace GrimService.Services.Rpcn
{
    public sealed class RpcnBridge
    {
        private readonly IOptionsMonitor<RpcnBridgeOptions> options;
        private readonly ConcurrentDictionary<string, byte> active = new();
        public RpcnBridge(IOptionsMonitor<RpcnBridgeOptions> options) { this.options = options; }
        public RpcnBridgeOptions Settings => options.CurrentValue;

        public RpcnBridgeDevice Authenticate(string base64Key)
        {
            var settings = Settings;
            if (!settings.Enabled || base64Key == null || base64Key.Length > 64) return null;
            byte[] key;
            try { key = Convert.FromBase64String(base64Key); }
            catch (FormatException) { return null; }
            if (key.Length != 32) return null;
            var hash = SHA256.HashData(key);
            CryptographicOperations.ZeroMemory(key);
            foreach (var device in settings.Devices ?? new())
            {
                if (!device.Enabled || string.IsNullOrWhiteSpace(device.Id) ||
                    string.IsNullOrEmpty(device.Username) || device.Username.Length > 16 ||
                    device.Username.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_') ||
                    device.KeySha256?.Length != 64) continue;
                byte[] expected;
                try { expected = Convert.FromHexString(device.KeySha256); }
                catch (FormatException) { continue; }
                if (CryptographicOperations.FixedTimeEquals(hash, expected)) return device;
            }
            return null;
        }

        public bool TryAcquire(RpcnBridgeDevice device) => active.TryAdd(device.Username, 0);
        public void Release(RpcnBridgeDevice device) => active.TryRemove(device.Username, out _);

        public async Task<(TcpClient Client, SslStream Stream, byte[] Greeting)> ConnectAsync(CancellationToken cancellation)
        {
            var cfg = Settings;
            if (string.IsNullOrWhiteSpace(cfg.Host) || cfg.Host.Contains('/') || cfg.Port < 1 || cfg.Port > 65535)
                throw new InvalidOperationException("Invalid configured RPCN endpoint.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(cfg.ConnectTimeoutSeconds, 1, 30)));
            var tcp = new TcpClient { NoDelay = true };
            SslStream tls = null;
            try
            {
                await tcp.ConnectAsync(cfg.Host, cfg.Port, timeout.Token);
                var pin = cfg.CertificateSha256;
                tls = new SslStream(tcp.GetStream(), false, (_, cert, _, errors) =>
                {
                    if (string.IsNullOrWhiteSpace(pin)) return errors == SslPolicyErrors.None;
                    if (cert == null || pin.Length != 64) return false;
                    try
                    {
                        return CryptographicOperations.FixedTimeEquals(
                            SHA256.HashData(cert.GetRawCertData()), Convert.FromHexString(pin));
                    }
                    catch (FormatException) { return false; }
                });
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = cfg.Host,
                    CertificateRevocationCheckMode = X509RevocationMode.Online
                }, timeout.Token);
                var greeting = await RpcnWire.ReadAsync(tls, timeout.Token);
                if (RpcnWire.ServerVersion(greeting) != cfg.ProtocolVersion)
                    throw new InvalidDataException("RPCN protocol version differs from configured version.");
                return (tcp, tls, greeting);
            }
            catch { tls?.Dispose(); tcp.Dispose(); throw; }
        }

        public async Task RelayAsync(WebSocket socket, Stream upstream, byte[] greeting,
            RpcnBridgeDevice device, CancellationToken cancellation)
        {
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            lifetime.CancelAfter(TimeSpan.FromMinutes(Math.Clamp(Settings.SessionMinutes, 1, 240)));
            var ct = lifetime.Token;
            await socket.SendAsync(new ArraySegment<byte>(greeting), WebSocketMessageType.Binary, true, ct);
            var outgoing = ToRpcn();
            var incoming = FromRpcn();
            await Task.WhenAny(outgoing, incoming);
            lifetime.Cancel();
            try { await Task.WhenAll(outgoing, incoming); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }

            async Task ToRpcn()
            {
                var buffer = new byte[16384];
                while (!ct.IsCancellationRequested)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult part;
                    do
                    {
                        part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (part.MessageType == WebSocketMessageType.Close) return;
                        if (part.MessageType != WebSocketMessageType.Binary ||
                            message.Length + part.Count > RpcnWire.MaximumPacketSize)
                            throw new InvalidDataException("Expected a bounded binary RPCN message.");
                        message.Write(buffer, 0, part.Count);
                    } while (!part.EndOfMessage);
                    var packet = message.ToArray();
                    RpcnWire.ValidateRequest(packet, device.Username);
                    await upstream.WriteAsync(packet.AsMemory(), ct);
                }
            }

            async Task FromRpcn()
            {
                while (!ct.IsCancellationRequested)
                {
                    var packet = await RpcnWire.ReadAsync(upstream, ct);
                    if (packet[0] != 1 && packet[0] != 2)
                        throw new InvalidDataException("Unexpected RPCN packet type.");
                    await socket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, ct);
                }
            }
        }
    }
}
