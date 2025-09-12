using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace KestrunTests.Jwt;

internal sealed class EcdsaEnablingCryptoProviderFactory : CryptoProviderFactory
{
    public override bool IsSupportedAlgorithm(string algorithm, SecurityKey key) => (IsEcdsaAlg(algorithm) && HasEcdsaKey(key)) || base.IsSupportedAlgorithm(algorithm, key);

    public override SignatureProvider CreateForSigning(SecurityKey key, string algorithm, bool willCreateSignatures = true)
    {
        if (IsEcdsaAlg(algorithm) && key is X509SecurityKey x509)
        {
            var ecdsa = x509.Certificate.GetECDsaPrivateKey();
            if (ecdsa != null)
            {
                return new AsymmetricSignatureProvider(new ECDsaSecurityKey(ecdsa) { KeyId = x509.KeyId ?? x509.Certificate.Thumbprint }, algorithm, willCreateSignatures);
            }
        }
        return base.CreateForSigning(key, algorithm, willCreateSignatures);
    }

    // no override for encryption; we keep JWE providers default

    private static bool IsEcdsaAlg(string alg) => alg is SecurityAlgorithms.EcdsaSha256 or SecurityAlgorithms.EcdsaSha384 or SecurityAlgorithms.EcdsaSha512;



    private static bool HasEcdsaKey(SecurityKey key)
        => key is ECDsaSecurityKey
           || (key is X509SecurityKey x && x.Certificate?.GetECDsaPrivateKey() != null);
}
