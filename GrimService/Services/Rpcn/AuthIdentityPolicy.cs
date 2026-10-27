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
                else if (incomingProvider != "rpcn") provider = incomingProvider;
                else return false;
            }
            bool ps3Switch = string.Equals(incomingPlatform, "PS3", StringComparison.OrdinalIgnoreCase) &&
                (provider == "psn" && incomingProvider == "rpcn" || provider == "rpcn" && incomingProvider == "psn");
            return (provider == incomingProvider || ps3Switch) &&
                (incomingProvider != "rpcn" || (incomingRpcnId > 0 && (!storedRpcnId.HasValue || storedRpcnId == incomingRpcnId)));
        }
    }
}
