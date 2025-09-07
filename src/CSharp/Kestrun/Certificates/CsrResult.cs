
using Org.BouncyCastle.Crypto;

namespace Kestrun.Certificates;
/// <summary>
/// Represents the result of creating a Certificate Signing Request (CSR), including the PEM-encoded CSR and the private key.
/// </summary>
/// <param name="CsrPem">The PEM-encoded CSR string.</param>
/// <param name="CsrDer">The DER-encoded CSR bytes.</param>
/// <param name="PrivateKey">The private key associated with the CSR.</param>
/// <param name="PrivateKeyPem">The PEM-encoded private key string.</param>
/// <param name="PrivateKeyDer">The DER-encoded private key bytes.</param>
/// <param name="PrivateKeyPemEncrypted">The PEM-encoded encrypted private key string, if an encryption password was provided; otherwise, null.</param>
/// <param name="PublicKeyPem">The PEM-encoded public key string.</param>
/// <param name="PublicKeyDer">The DER-encoded public key bytes.</param>
public record CsrResult(
    // CSR
    string CsrPem,
    byte[] CsrDer,

    // Private key
    AsymmetricKeyParameter PrivateKey, // for programmatic use
    string PrivateKeyPem,              // -----BEGIN PRIVATE KEY-----
    byte[] PrivateKeyDer,              // PKCS#8 DER
    string? PrivateKeyPemEncrypted,    // -----BEGIN ENCRYPTED PRIVATE KEY----- (if password provided)

    // Public key
    string PublicKeyPem,               // -----BEGIN PUBLIC KEY-----
    byte[] PublicKeyDer
);
