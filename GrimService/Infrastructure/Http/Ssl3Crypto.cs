using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace GrimService.Infrastructure.Http
{


    internal sealed class Ssl3Crypto : BcTlsCrypto
    {
        public Ssl3Crypto() : base(new SecureRandom()) { }

        public override bool HasEncryptionAlgorithm(int algorithm) =>
            algorithm == EncryptionAlgorithm.RC4_128 || base.HasEncryptionAlgorithm(algorithm);

        public override TlsCipher CreateCipher(TlsCryptoParameters parameters, int encryptionAlgorithm, int macAlgorithm)
        {
            if (encryptionAlgorithm != EncryptionAlgorithm.RC4_128)
                return base.CreateCipher(parameters, encryptionAlgorithm, macAlgorithm);

            if (!parameters.ServerVersion.IsSsl ||
                (macAlgorithm != MacAlgorithm.hmac_sha1 && macAlgorithm != MacAlgorithm.hmac_md5))
                throw new TlsFatalAlert(AlertDescription.internal_error);

            return new Ssl3Rc4Cipher(parameters, CreateMac(parameters, macAlgorithm), CreateMac(parameters, macAlgorithm));
        }



        private sealed class Ssl3Rc4Cipher : TlsNullCipher
        {
            private readonly RC4Engine incoming = new();
            private readonly RC4Engine outgoing = new();

            public Ssl3Rc4Cipher(TlsCryptoParameters parameters, TlsHmac clientMac, TlsHmac serverMac)
                : base(parameters, clientMac, serverMac)
            {
                int keyOffset = clientMac.MacLength + serverMac.MacLength;


                byte[] keys = TlsImplUtilities.CalculateKeyBlock(parameters, keyOffset + 32);
                try
                {
                    var clientKey = new KeyParameter(keys, keyOffset, 16);
                    var serverKey = new KeyParameter(keys, keyOffset + 16, 16);
                    incoming.Init(false, parameters.IsServer ? clientKey : serverKey);
                    outgoing.Init(true, parameters.IsServer ? serverKey : clientKey);
                }
                finally { CryptographicOperations.ZeroMemory(keys); }
            }

            public override TlsEncodeResult EncodePlaintext(long sequence, short type, ProtocolVersion version,
                int headerAllocation, byte[] plaintext, int offset, int length) =>
                EncodePlaintext(sequence, type, version, headerAllocation, plaintext.AsSpan(offset, length));

            public override TlsEncodeResult EncodePlaintext(long sequence, short type, ProtocolVersion version,
                int headerAllocation, ReadOnlySpan<byte> plaintext)
            {
                var record = base.EncodePlaintext(sequence, type, version, headerAllocation, plaintext);
                int start = record.off + headerAllocation;
                outgoing.ProcessBytes(record.buf, start, record.len - headerAllocation, record.buf, start);
                return record;
            }

            public override TlsDecodeResult DecodeCiphertext(long sequence, short type, ProtocolVersion version,
                byte[] ciphertext, int offset, int length)
            {
                if (length < m_readMac.Size) throw new TlsFatalAlert(AlertDescription.decode_error);
                incoming.ProcessBytes(ciphertext, offset, length, ciphertext, offset);
                return base.DecodeCiphertext(sequence, type, version, ciphertext, offset, length);
            }
        }
    }
}
