using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GrimService.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrimService.Services.Rpcn
{

    public sealed class RpcnRelayService : BackgroundService
    {
        private readonly RpcnRelayOptions cfg;
        private readonly ILogger<RpcnRelayService> log;
        public RpcnRelayService(IOptions<RpcnRelayOptions> options, ILogger<RpcnRelayService> logger)
        { cfg = options.Value; log = logger; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!cfg.Enabled) return;
            TcpListener listener = null;
            var sessions = new ConcurrentDictionary<Guid, Task>();
            using var listenerLifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            try
            {
                var address = IPAddress.Parse(cfg.ListenAddress);
                var allowed = cfg.AllowedClientIps.Select(x => IPAddress.Parse(x).MapToIPv6()).ToArray();
                if (!allowed.Any() || cfg.Port < 1024 || cfg.Port > 65535 || cfg.UpstreamPort < 1 || cfg.UpstreamPort > 65535 ||
                    string.IsNullOrWhiteSpace(cfg.UpstreamHost) || cfg.UpstreamHost.Contains('/') || string.IsNullOrWhiteSpace(cfg.CertificatePath))
                    throw new InvalidOperationException("Invalid relay configuration");
                byte[] pin = string.IsNullOrWhiteSpace(cfg.UpstreamCertificateSha256) ? null : Convert.FromHexString(cfg.UpstreamCertificateSha256);
                if (pin != null && pin.Length != 32) throw new InvalidOperationException("Invalid upstream pin");
                using var certificate = new X509Certificate2(cfg.CertificatePath, cfg.CertificatePassword,
                    OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
                if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow) throw new InvalidOperationException("Invalid relay certificate");
                Directory.CreateDirectory(cfg.LogDirectory);
                listener = new TcpListener(address, cfg.Port); listener.Start(8);
                log.LogWarning("Temporary RPCN diagnostic relay enabled on {Address}:{Port}; exact IP allowlist, TLS on both legs", address, cfg.Port);
                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var client = await listener.AcceptTcpClientAsync(stoppingToken);
                        var remote = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.MapToIPv6();
                        if (remote == null || !allowed.Contains(remote) || sessions.Count >= Math.Clamp(cfg.MaxConnections, 1, 4)) { client.Dispose(); continue; }
                        var id = Guid.NewGuid();
                        var task = RunSession(client, certificate, pin, id, listenerLifetime.Token);
                        sessions[id] = task;
                        _ = ReleaseAsync(id, task);
                    }
                }
                finally
                {
                    listener.Stop();
                    listenerLifetime.Cancel();
                    await Task.WhenAll(sessions.Values);
                }
                async Task ReleaseAsync(Guid id, Task task)
                { try { await task; } finally { sessions.TryRemove(id, out _); } }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {

                log.LogError("RPCN relay disabled after {ErrorType}; check configuration/certificate/listener", ex.GetType().Name);
            }
            finally { listener?.Stop(); }
        }

        private async Task RunSession(TcpClient client, X509Certificate2 certificate, byte[] pin, Guid id, CancellationToken stop)
        {
            using (client)
            using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stop))
            {
                lifetime.CancelAfter(TimeSpan.FromMinutes(Math.Clamp(cfg.SessionMinutes, 1, 60)));
                var ct = lifetime.Token;
                try
                {
                    client.NoDelay = true;
                    using var downstream = new SslStream(client.GetStream(), false);
                    using var upstreamTcp = new TcpClient { NoDelay = true };
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    deadline.CancelAfter(TimeSpan.FromSeconds(15));
                    await downstream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                        ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12,
                        ClientCertificateRequired = false
                    }, deadline.Token);
                    await upstreamTcp.ConnectAsync(cfg.UpstreamHost, cfg.UpstreamPort, deadline.Token);
                    using var upstream = new SslStream(upstreamTcp.GetStream(), false, (_, cert, _, errors) =>
                        cert != null && (pin == null ? errors == SslPolicyErrors.None :
                            CryptographicOperations.FixedTimeEquals(SHA256.HashData(cert.GetRawCertData()), pin)));
                    await upstream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                        TargetHost = cfg.UpstreamHost, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.Online
                    }, deadline.Token);
                    var greeting = await RpcnWire.ReadAsync(upstream, deadline.Token);
                    uint version = RpcnWire.ServerVersion(greeting);
                    await downstream.WriteAsync(greeting, deadline.Token);
                    string path = Path.Combine(cfg.LogDirectory, $"rpcn-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{id:N}.jsonl");
                    var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read, Options = FileOptions.Asynchronous };
                    if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    await using var file = new FileStream(path, fileOptions);
                    await using var writer = new StreamWriter(file, new UTF8Encoding(false)) { AutoFlush = true };
                    using var writeLock = new SemaphoreSlim(1);
                    long written = 0, limit = (long)Math.Clamp(cfg.MaxLogMegabytes, 1, 32) * 1024 * 1024;
                    await writer.WriteLineAsync($"{{\"protocol\":{version},\"session\":\"{id:N}\",\"authPayloads\":\"omitted\"}}");
                    log.LogInformation("RPCN relay session {Session} established; protocol={Version}", id, version);
                    Task a = Pump(downstream, upstream, true), b = Pump(upstream, downstream, false);
                    await Task.WhenAny(a, b);
                    lifetime.Cancel(); client.Close(); upstreamTcp.Close();
                    try { await Task.WhenAll(a, b); } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }

                    async Task Pump(Stream source, Stream target, bool fromClient)
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            idle.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(cfg.IdleSeconds, 30, 600)));
                            var packet = await RpcnWire.ReadAsync(source, idle.Token);
                            try
                            {
                                if ((fromClient && packet[0] != 0) || (!fromClient && packet[0] != 1 && packet[0] != 2 && packet[0] != 3))
                                    throw new InvalidDataException("Invalid frame direction");
                                var line = RpcnRelayTrace.Describe(packet, fromClient, version == 32);
                                await writeLock.WaitAsync(ct);
                                try { if (written < limit) { written += Encoding.UTF8.GetByteCount(line) + 1; await writer.WriteLineAsync(line); } }
                                finally { writeLock.Release(); }

                                await target.WriteAsync(packet, ct);
                            }
                            finally { CryptographicOperations.ZeroMemory(packet); }
                        }
                    }
                }
                catch (Exception ex)
                { log.LogInformation("RPCN relay session {Session} closed: {ErrorType}, code={Code}", id, ex.GetType().Name, ex.HResult); }
            }
        }
    }
}
