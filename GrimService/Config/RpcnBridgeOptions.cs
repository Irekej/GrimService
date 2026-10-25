using System.Collections.Generic;

namespace GrimService.Config
{
    public sealed class RpcnBridgeOptions
    {
        public const string Section = "RpcnBridge";
        public bool Enabled { get; set; }
        public string Host { get; set; } = "np.rpcs3.net";
        public int Port { get; set; } = 31313;
        public uint ProtocolVersion { get; set; } = 32;
        public int ConnectTimeoutSeconds { get; set; } = 10;
        public int SessionMinutes { get; set; } = 60;

        public string CertificateSha256 { get; set; }
        public List<RpcnBridgeDevice> Devices { get; set; } = new();



        public List<string> AllowedTicketServiceIds { get; set; } = new() { "", "GrimService-GT5", "GT5_JP3010", "GrimService-GT6" };
        public string TicketPublicKeyPem { get; set; } =
            "-----BEGIN PUBLIC KEY-----\nME4wEAYHKoZIzj0CAQYFK4EEACADOgAEsHvA8K3bl2V+nziQOejSucl9wqMdMELn\n0Eebk9gcQrCr32xCGRox4x+TNC+PAzvVKcLFf9taCn0=\n-----END PUBLIC KEY-----";
    }

    public sealed class RpcnBridgeDevice
    {
        public string Id { get; set; }
        public string KeySha256 { get; set; }

        public string Username { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
