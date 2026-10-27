using System;

namespace GrimService.Services.Rpcn
{
    public static class AuthIdentityPolicy
    {
        public static bool Allows(string storedProvider, string storedPlatform, long? storedRpcnId,
            string incomingProvider, long? incomingRpcnId, string incomingPlatform = null)
        {
            var provider = storedProvider?.ToLowerInvariant();


            if (string.IsNullOrEmpty(provider)) {
                if (string.Equals(storedPlatform, "RPCS3", StringComparison.OrdinalIgnoreCase)) provider = "rpcn";
                else if (string.Equals(storedPlatform, "PS3", StringComparison.OrdinalIgnoreCase)) provider = "psn";
                else provider = incomingProvider;
            }
            bool ps3 = string.Equals(incomingPlatform, "PS3", StringComparison.OrdinalIgnoreCase);
            bool rpcs3 = string.Equals(incomingPlatform, "RPCS3", StringComparison.OrdinalIgnoreCase);
            bool providerSwitch = provider == "psn" && incomingProvider == "rpcn" && (ps3 || rpcs3)
                || provider == "rpcn" && incomingProvider == "psn" && ps3;
            return (provider == incomingProvider || providerSwitch) &&
                (incomingProvider != "rpcn" || (incomingRpcnId > 0 && (!storedRpcnId.HasValue || storedRpcnId == incomingRpcnId)));
        }
    }
}
