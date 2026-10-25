using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GrimService.Infrastructure.Security;

public static class GameSigningKey
{
    public static SymmetricSecurityKey Read(IConfiguration configuration, ILogger logger = null)
    {
        string secret = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Jwt:Key must be configured before issuing game sessions.");

        byte[] material = Encoding.UTF8.GetBytes(secret);
        return new SymmetricSecurityKey(material) { CryptoProviderFactory = LegacyHmac };
    }

    private static readonly CryptoProviderFactory LegacyHmac = new OriginalHs256Factory();

    private sealed class OriginalHs256Factory : CryptoProviderFactory
    {
        public override KeyedHashAlgorithm CreateKeyedHashAlgorithm(byte[] keyBytes, string algorithm)
            => algorithm is SecurityAlgorithms.HmacSha256 or SecurityAlgorithms.HmacSha256Signature
                ? new HMACSHA256(keyBytes)
                : base.CreateKeyedHashAlgorithm(keyBytes, algorithm);
    }
}
