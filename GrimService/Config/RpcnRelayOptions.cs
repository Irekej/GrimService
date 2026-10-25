using System.Collections.Generic;

namespace GrimService.Config
{

    public sealed class RpcnRelayOptions
    {
        public const string Section = "RpcnRelay";
        public bool Enabled { get; set; }
        public string ListenAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 31313;
        public List<string> AllowedClientIps { get; set; } = new();
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        public string UpstreamHost { get; set; } = "np.rpcs3.net";
        public int UpstreamPort { get; set; } = 31313;
        public string UpstreamCertificateSha256 { get; set; }
        public int MaxConnections { get; set; } = 2;
        public int SessionMinutes { get; set; } = 20;
        public int IdleSeconds { get; set; } = 120;
        public int MaxLogMegabytes { get; set; } = 8;
        public string LogDirectory { get; set; } = "Logs/RpcnRelay";

        public bool EnableRegistrationRelay { get; set; }
        public int RegistrationListenPort { get; set; } = 3657;
        public int RegistrationUpstreamPort { get; set; } = 3657;
    }
}
