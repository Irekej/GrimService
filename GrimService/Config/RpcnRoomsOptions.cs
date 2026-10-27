namespace GrimService.Config
{
    public sealed class RpcnRoomsOptions
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = "np.rpcs3.net";
        public int Port { get; set; } = 31313;
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Token { get; set; } = "";
        public string CertificateSha256 { get; set; } = "7028AD2117139EEA9E1FE14713D0CB4FCD8815B2F39AB4E10FF6725A38AFD155";
        public int RefreshSeconds { get; set; } = 45;
        public string GT5CommunicationId { get; set; } = "NPWR00651_00";
        public string GT6CommunicationId { get; set; } = "NPWR04712_00";
    }
}
