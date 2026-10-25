using System;

namespace GrimService.Services.Rpcn
{
    public static class AuthIdentityPolicy
    {
        public static bool Allows(string storedProvider, string storedPlatform, long? storedRpcnId,
            string incomingProvider, long? incomingRpcnId)
        {
            var provider = storedProvider?.ToLowerInvariant();


            if (string.IsNullOrEmpty(provider)) {
                if (string.Equals(storedPlatform, "RPCS3", StringComparison.OrdinalIgnoreCase)) provider = "rpcn";
                else if (string.Equals(storedPlatform, "PS3", StringComparison.OrdinalIgnoreCase)) provider = "psn";
                else if (incomingProvider != "rpcn") provider = incomingProvider;
                else return false;
            }
            return provider == incomingProvider &&
                (provider != "rpcn" || (incomingRpcnId > 0 && (!storedRpcnId.HasValue || storedRpcnId == incomingRpcnId)));
        }
    }
}
