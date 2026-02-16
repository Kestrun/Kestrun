using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using System.Text;
using Org.BouncyCastle.Asn1.X9;
using Serilog;
using Kestrun.Utilities;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Text.Json.Serialization;


namespace Kestrun.Certificates;

/// <summary>
/// Drop-in replacement for Pode’s certificate helpers, powered by Bouncy Castle.
/// </summary>
public static class CertificateManager
{
    /// <summary>
    /// Controls whether the private key is appended to the certificate PEM file in addition to
    /// writing a separate .key file. Appending was initially added to work around platform
    /// inconsistencies when importing encrypted PEM pairs on some Linux runners. However, having
    /// both a combined (cert+key) file and a separate key file can itself introduce ambiguity in
    /// which API path <see cref="X509Certificate2"/> chooses (single-file vs dual-file), which was
    /// observed to contribute to rare flakiness (private key occasionally not attached after
    /// import). To make behavior deterministic we now disable appending by default and allow it to
    /// be re-enabled explicitly via the environment variable KESTRUN_APPEND_KEY_TO_PEM.
    /// Set KESTRUN_APPEND_KEY_TO_PEM=1 (or "true") to re-enable.
    /// </summary>
    private static bool ShouldAppendKeyToPem =>
        string.Equals(Environment.GetEnvironmentVariable("KESTRUN_APPEND_KEY_TO_PEM"), "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("KESTRUN_APPEND_KEY_TO_PEM"), "true", StringComparison.OrdinalIgnoreCase);

    #region  Self-signed certificate
    /// <summary>
    /// Creates a new self-signed X509 certificate using the specified options.
    /// </summary>
    /// <param name="o">Options for creating the self-signed certificate.</param>
    /// <returns>A new self-signed X509Certificate2 instance.</returns>
    public static X509Certificate2 NewSelfSigned(SelfSignedOptions o)
    {
        var random = new SecureRandom(new CryptoApiRandomGenerator());

        // ── 1. Key pair ───────────────────────────────────────────────────────────
        var keyPair =
            o.KeyType switch
            {
                KeyType.Rsa => GenRsaKeyPair(o.KeyLength, random),
                KeyType.Ecdsa => GenEcKeyPair(o.KeyLength, random),
                _ => throw new ArgumentOutOfRangeException()
            };

        // ── 2. Certificate body ───────────────────────────────────────────────────
        var notBefore = DateTime.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(o.ValidDays);
        var serial = BigIntegers.CreateRandomInRange(
                            BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);

        var subjectDn = new X509Name($"CN={o.DnsNames.First()}");
        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(serial);
        gen.SetIssuerDN(subjectDn);
        gen.SetSubjectDN(subjectDn);
        gen.SetNotBefore(notBefore);
        gen.SetNotAfter(notAfter);
        gen.SetPublicKey(keyPair.Public);

        // SANs
        var altNames = o.DnsNames
                        .Select(n => new GeneralName(
                            IPAddress.TryParse(n, out _) ?
                                GeneralName.IPAddress : GeneralName.DnsName, n))
                        .ToArray();
        gen.AddExtension(X509Extensions.SubjectAlternativeName, false,
                         new DerSequence(altNames));

        // EKU
        var eku = o.Purposes ??
         [
             KeyPurposeID.id_kp_serverAuth,
            KeyPurposeID.id_kp_clientAuth
         ];
        gen.AddExtension(X509Extensions.ExtendedKeyUsage, false,
                         new ExtendedKeyUsage([.. eku]));

        // KeyUsage – allow digitalSignature & keyEncipherment
        gen.AddExtension(X509Extensions.KeyUsage, true,
                         new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

        // ── 3. Sign & output ──────────────────────────────────────────────────────
        var sigAlg = o.KeyType == KeyType.Rsa ? "SHA256WITHRSA" : "SHA384WITHECDSA";
        var signer = new Asn1SignatureFactory(sigAlg, keyPair.Private, random);
        var cert = gen.Generate(signer);

        return ToX509Cert2(cert, keyPair.Private,
            o.Exportable ? X509KeyStorageFlags.Exportable : X509KeyStorageFlags.DefaultKeySet,
            o.Ephemeral);
    }
    #endregion

    #region  CSR

    /// <summary>
    /// Creates a new Certificate Signing Request (CSR) and returns the PEM-encoded CSR and the private key.
    /// </summary>
    /// <param name="options">The options for the CSR.</param>
    /// <param name="encryptionPassword">The password to encrypt the private key, if desired.</param>
    /// <returns>A <see cref="CsrResult"/> containing the CSR and private key information.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static CsrResult NewCertificateRequest(CsrOptions options, ReadOnlySpan<char> encryptionPassword = default)
    {
        // 0️⃣ Keypair
        var random = new SecureRandom(new CryptoApiRandomGenerator());
        var keyPair = options.KeyType switch
        {
            KeyType.Rsa => GenRsaKeyPair(options.KeyLength, random),
            KeyType.Ecdsa => GenEcKeyPair(options.KeyLength, random),
            _ => throw new ArgumentOutOfRangeException(nameof(options.KeyType))
        };

        // 1️⃣ Subject DN
        var order = new List<DerObjectIdentifier>();
        var attrs = new Dictionary<DerObjectIdentifier, string>();
        void Add(DerObjectIdentifier oid, string? v)
        {
            if (!string.IsNullOrWhiteSpace(v)) { order.Add(oid); attrs[oid] = v; }
        }
        Add(X509Name.C, options.Country);
        Add(X509Name.O, options.Org);
        Add(X509Name.OU, options.OrgUnit);
        Add(X509Name.CN, options.CommonName ?? options.DnsNames.First());
        var subject = new X509Name(order, attrs);

        // 2️⃣ SAN extension
        var altNames = options.DnsNames
            .Select(d => new GeneralName(
                IPAddress.TryParse(d, out _)
                    ? GeneralName.IPAddress
                    : GeneralName.DnsName, d))
            .ToArray();
        var sanSeq = new DerSequence(altNames);

        var extGen = new X509ExtensionsGenerator();
        extGen.AddExtension(X509Extensions.SubjectAlternativeName, false, sanSeq);
        var extensions = extGen.Generate();

        var extensionRequestAttr = new AttributePkcs(
            PkcsObjectIdentifiers.Pkcs9AtExtensionRequest,
            new DerSet(extensions));
        var attrSet = new DerSet(extensionRequestAttr);

        // 3️⃣ CSR
        var sigAlg = options.KeyType == KeyType.Rsa ? "SHA256WITHRSA" : "SHA384WITHECDSA";
        var csr = new Pkcs10CertificationRequest(sigAlg, subject, keyPair.Public, attrSet, keyPair.Private);

        // 4️⃣ CSR PEM + DER
        string csrPem;
        using (var sw = new StringWriter())
        {
            new PemWriter(sw).WriteObject(csr);
            csrPem = sw.ToString();
        }
        var csrDer = csr.GetEncoded();

        // 5️⃣ Private key PEM + DER
        string privateKeyPem;
        using (var sw = new StringWriter())
        {
            new PemWriter(sw).WriteObject(keyPair.Private);
            privateKeyPem = sw.ToString();
        }
        var pkInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private);
        var privateKeyDer = pkInfo.GetEncoded();

        // 6️⃣ Optional encrypted PEM
        string? privateKeyPemEncrypted = null;
        if (!encryptionPassword.IsEmpty)
        {
            var pwd = encryptionPassword.ToArray(); // BC requires char[]
            try
            {
                var gen = new Pkcs8Generator(keyPair.Private, Pkcs8Generator.PbeSha1_3DES)
                {
                    Password = pwd
                };
                using var encSw = new StringWriter();
                new PemWriter(encSw).WriteObject(gen);
                privateKeyPemEncrypted = encSw.ToString();
            }
            finally
            {
                Array.Clear(pwd, 0, pwd.Length); // wipe memory
            }
        }

        // 7️⃣ Public key PEM + DER
        var spki = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public);
        var publicKeyDer = spki.GetEncoded();
        string publicKeyPem;
        using (var sw = new StringWriter())
        {
            new PemWriter(sw).WriteObject(spki);
            publicKeyPem = sw.ToString();
        }

        return new CsrResult(
            csrPem,
            csrDer,
            keyPair.Private,
            privateKeyPem,
            privateKeyDer,
            privateKeyPemEncrypted,
            publicKeyPem,
            publicKeyDer
        );
    }


    #endregion

    #region  Import
    /// <summary>
    /// Imports an X509 certificate from the specified file path, with optional password and private key file.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="password">The password for the certificate, if required.</param>
    /// <param name="privateKeyPath">The path to the private key file, if separate.</param>
    /// <param name="flags">Key storage flags for the imported certificate.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    public static X509Certificate2 Import(
       string certPath,
       ReadOnlySpan<char> password = default,
       string? privateKeyPath = null,
       X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable)
    {
        ValidateImportInputs(certPath, privateKeyPath);

        var ext = Path.GetExtension(certPath).ToLowerInvariant();
        return ext switch
        {
            ".pfx" or ".p12" => ImportPfx(certPath, password, flags),
            ".cer" or ".der" => ImportDer(certPath),
            ".pem" or ".crt" => ImportPem(certPath, password, privateKeyPath),
            _ => throw new NotSupportedException($"Certificate extension '{ext}' is not supported.")
        };
    }

    /// <summary>
    /// Validates the inputs for importing a certificate.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="privateKeyPath">The path to the private key file, if separate.</param>
    private static void ValidateImportInputs(string certPath, string? privateKeyPath)
    {
        if (string.IsNullOrEmpty(certPath))
        {
            throw new ArgumentException("Certificate path cannot be null or empty.", nameof(certPath));
        }
        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException("Certificate file not found.", certPath);
        }
        if (!string.IsNullOrEmpty(privateKeyPath) && !File.Exists(privateKeyPath))
        {
            throw new FileNotFoundException("Private key file not found.", privateKeyPath);
        }
    }

    /// <summary>
    /// Imports a PFX certificate from the specified file path.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="password">The password for the certificate, if required.</param>
    /// <param name="flags">Key storage flags for the imported certificate.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    private static X509Certificate2 ImportPfx(string certPath, ReadOnlySpan<char> password, X509KeyStorageFlags flags)
#if NET9_0_OR_GREATER
        => X509CertificateLoader.LoadPkcs12FromFile(certPath, password, flags, Pkcs12LoaderLimits.Defaults);
#else
        => new(File.ReadAllBytes(certPath), password, flags);
#endif

    private static X509Certificate2 ImportDer(string certPath)
#if NET9_0_OR_GREATER
        => X509CertificateLoader.LoadCertificateFromFile(certPath);
#else
        => new(File.ReadAllBytes(certPath));
#endif


    /// <summary>
    /// Imports a PEM certificate from the specified file path.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="password">The password for the certificate, if required.</param>
    /// <param name="privateKeyPath">The path to the private key file, if separate.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    private static X509Certificate2 ImportPem(string certPath, ReadOnlySpan<char> password, string? privateKeyPath)
    {
        // No separate key file provided
        if (string.IsNullOrEmpty(privateKeyPath))
        {
            return password.IsEmpty
                ? LoadCertOnlyPem(certPath)
                : X509Certificate2.CreateFromEncryptedPemFile(certPath, password);
        }

        // Separate key file provided
        return password.IsEmpty
            ? ImportPemUnencrypted(certPath, privateKeyPath)
            : ImportPemEncrypted(certPath, password, privateKeyPath);
    }

    /// <summary>
    /// Imports an unencrypted PEM certificate from the specified file path.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="privateKeyPath">The path to the private key file.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    private static X509Certificate2 ImportPemUnencrypted(string certPath, string privateKeyPath)
        => X509Certificate2.CreateFromPemFile(certPath, privateKeyPath);

    /// <summary>
    /// Imports a PEM certificate from the specified file path.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="password">The password for the certificate, if required.</param>
    /// <param name="privateKeyPath">The path to the private key file, if separate.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    private static X509Certificate2 ImportPemEncrypted(string certPath, ReadOnlySpan<char> password, string privateKeyPath)
    {
        // Prefer single-file path (combined) first for reliability on some platforms
        try
        {
            var single = X509Certificate2.CreateFromEncryptedPemFile(certPath, password);
            if (single.HasPrivateKey)
            {
                Log.Debug("Imported encrypted PEM using single-file path (combined cert+key) for {CertPath}", certPath);
                return single;
            }
        }
        catch (Exception exSingle)
        {
            Log.Debug(exSingle, "Single-file encrypted PEM import failed, falling back to separate key file {KeyFile}", privateKeyPath);
        }

        var loaded = X509Certificate2.CreateFromEncryptedPemFile(certPath, password, privateKeyPath);

        if (loaded.HasPrivateKey)
        {
            return loaded;
        }

        // Fallback manual pairing if platform failed to associate the key
        TryManualEncryptedPemPairing(certPath, password, privateKeyPath, ref loaded);
        return loaded;
    }

    /// <summary>
    /// Tries to manually pair an encrypted PEM certificate with its private key.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="password">The password for the certificate, if required.</param>
    /// <param name="privateKeyPath">The path to the private key file, if separate.</param>
    /// <param name="loaded">The loaded X509Certificate2 instance.</param>
    private static void TryManualEncryptedPemPairing(string certPath, ReadOnlySpan<char> password, string privateKeyPath, ref X509Certificate2 loaded)
    {
        try
        {
            var certOnly = LoadCertOnlyPem(certPath);
            var encDer = ExtractEncryptedPemDer(privateKeyPath);

            if (encDer is null)
            {
                Log.Debug("Encrypted PEM manual pairing fallback skipped: markers not found in key file {KeyFile}", privateKeyPath);
                return;
            }

            var lastErr = TryPairCertificateWithKey(certOnly, password, encDer, ref loaded);

            if (lastErr != null)
            {
                Log.Debug(lastErr, "Encrypted PEM manual pairing attempts failed (all rounds); returning original loaded certificate without private key");
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Encrypted PEM manual pairing fallback failed unexpectedly; returning original loaded certificate without private key");
        }
    }

    /// <summary>
    /// Extracts the encrypted PEM DER bytes from a private key file.
    /// </summary>
    /// <param name="privateKeyPath">The path to the private key file.</param>
    /// <returns>The DER bytes if successful, null otherwise.</returns>
    private static byte[]? ExtractEncryptedPemDer(string privateKeyPath)
    {
        const string encBegin = "-----BEGIN ENCRYPTED PRIVATE KEY-----";
        const string encEnd = "-----END ENCRYPTED PRIVATE KEY-----";

        byte[]? encDer = null;
        for (var attempt = 0; attempt < 5 && encDer is null; attempt++)
        {
            var keyPem = File.ReadAllText(privateKeyPath);
            var start = keyPem.IndexOf(encBegin, StringComparison.Ordinal);
            var end = keyPem.IndexOf(encEnd, StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                start += encBegin.Length;
                var b64 = keyPem[start..end].Replace("\r", "").Replace("\n", "").Trim();
                try { encDer = Convert.FromBase64String(b64); }
                catch (FormatException fe)
                {
                    Log.Debug(fe, "Base64 decode failed on attempt {Attempt} reading encrypted key; retrying", attempt + 1);
                }
            }
            if (encDer is null)
            {
                Thread.Sleep(40 * (attempt + 1));
            }
        }

        return encDer;
    }

    /// <summary>
    /// Attempts to pair a certificate with an encrypted private key using RSA and ECDSA.
    /// </summary>
    /// <param name="certOnly">The certificate without a private key.</param>
    /// <param name="password">The password for the encrypted key.</param>
    /// <param name="encDer">The encrypted DER bytes.</param>
    /// <param name="loaded">The loaded certificate (updated if pairing succeeds).</param>
    /// <returns>The last exception encountered, or null if pairing succeeded.</returns>
    private static Exception? TryPairCertificateWithKey(X509Certificate2 certOnly, ReadOnlySpan<char> password, byte[] encDer, ref X509Certificate2 loaded)
    {
        Exception? lastErr = null;
        for (var round = 0; round < 2; round++)
        {
            if (TryPairWithRsa(certOnly, password, encDer, round, ref loaded, ref lastErr))
            {
                return null;
            }

            if (TryPairWithEcdsa(certOnly, password, encDer, round, ref loaded, ref lastErr))
            {
                return null;
            }

            Thread.Sleep(25 * (round + 1));
        }
        return lastErr;
    }

    /// <summary>
    /// Tries to pair a certificate with an RSA private key.
    /// </summary>
    /// <param name="certOnly">The certificate without a private key.</param>
    /// <param name="password">The password for the encrypted key.</param>
    /// <param name="encDer">The encrypted DER bytes.</param>
    /// <param name="round">The attempt round number.</param>
    /// <param name="loaded">The loaded certificate (updated if pairing succeeds).</param>
    /// <param name="lastErr">The last exception encountered (updated on failure).</param>
    /// <returns>True if pairing succeeded, false otherwise.</returns>
    private static bool TryPairWithRsa(X509Certificate2 certOnly, ReadOnlySpan<char> password, byte[] encDer, int round, ref X509Certificate2 loaded, ref Exception? lastErr)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportEncryptedPkcs8PrivateKey(password, encDer, out _);
            var withKey = certOnly.CopyWithPrivateKey(rsa);
            if (withKey.HasPrivateKey)
            {
                Log.Debug("Encrypted PEM manual pairing succeeded with RSA private key (round {Round}).", round + 1);
                loaded = withKey;
                return true;
            }
        }
        catch (Exception exRsa)
        {
            lastErr = lastErr is null ? exRsa : new AggregateException(lastErr, exRsa);
        }
        return false;
    }

    /// <summary>
    /// Tries to pair a certificate with an ECDSA private key.
    /// </summary>
    /// <param name="certOnly">The certificate without a private key.</param>
    /// <param name="password">The password for the encrypted key.</param>
    /// <param name="encDer">The encrypted DER bytes.</param>
    /// <param name="round">The attempt round number.</param>
    /// <param name="loaded">The loaded certificate (updated if pairing succeeds).</param>
    /// <param name="lastErr">The last exception encountered (updated on failure).</param>
    /// <returns>True if pairing succeeded, false otherwise.</returns>
    private static bool TryPairWithEcdsa(X509Certificate2 certOnly, ReadOnlySpan<char> password, byte[] encDer, int round, ref X509Certificate2 loaded, ref Exception? lastErr)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportEncryptedPkcs8PrivateKey(password, encDer, out _);
            var withKey = certOnly.CopyWithPrivateKey(ecdsa);
            if (withKey.HasPrivateKey)
            {
                Log.Debug("Encrypted PEM manual pairing succeeded with ECDSA private key (round {Round}).", round + 1);
                loaded = withKey;
                return true;
            }
        }
        catch (Exception exEc)
        {
            lastErr = lastErr is null ? exEc : new AggregateException(lastErr, exEc);
        }
        return false;
    }

    /// <summary>
    /// Loads a certificate from a PEM file that contains *only* a CERTIFICATE block (no key).
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <returns>The loaded X509Certificate2 instance.</returns>
    private static X509Certificate2 LoadCertOnlyPem(string certPath)
    {
        // 1) Read + trim the whole PEM text
        var pem = File.ReadAllText(certPath).Trim();

        // 2) Define the BEGIN/END markers
        const string begin = "-----BEGIN CERTIFICATE-----";
        const string end = "-----END CERTIFICATE-----";

        // 3) Find their positions
        var start = pem.IndexOf(begin, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidDataException("BEGIN CERTIFICATE marker not found");
        }

        start += begin.Length;

        var stop = pem.IndexOf(end, start, StringComparison.Ordinal);
        if (stop < 0)
        {
            throw new InvalidDataException("END CERTIFICATE marker not found");
        }

        // 4) Extract, clean, and decode the Base64 payload
        var b64 = pem[start..stop]
                       .Replace("\r", "")
                       .Replace("\n", "")
                       .Trim();
        var der = Convert.FromBase64String(b64);

        // 5) Return the X509Certificate2

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(der);
#else
        // .NET 8 or earlier path, using X509Certificate2 ctor
        // Note: this will not work in .NET 9+ due to the new X509CertificateLoader API
        //       which requires a byte array or a file path.
        return new X509Certificate2(der);
#endif
    }

    /// <summary>
    /// Imports an X509 certificate from the specified file path, using a SecureString password and optional private key file.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="password">The SecureString password for the certificate, if required.</param>
    /// <param name="privateKeyPath">The path to the private key file, if separate.</param>
    /// <param name="flags">Key storage flags for the imported certificate.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    public static X509Certificate2 Import(
       string certPath,
       SecureString password,
       string? privateKeyPath = null,
       X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable)
    {
        X509Certificate2? result = null;
        Log.Debug("Importing certificate from {CertPath} with flags {Flags}", certPath, flags);
        // ToSecureSpan zero-frees its buffer as soon as this callback returns.
        password.ToSecureSpan(span =>
        {
            // capture the return value of the span-based overload
            result = Import(certPath: certPath, password: span, privateKeyPath: privateKeyPath, flags: flags);
        });

        // at this point, unmanaged memory is already zeroed
        return result!;   // non-null because the callback always runs exactly once
    }

    /// <summary>
    /// Imports an X509 certificate from the specified file path, with optional private key file and key storage flags.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <param name="privateKeyPath">The path to the private key file, if separate.</param>
    /// <param name="flags">Key storage flags for the imported certificate.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    public static X509Certificate2 Import(
         string certPath,
         string? privateKeyPath = null,
         X509KeyStorageFlags flags = X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable)
    {
        // ToSecureSpan zero-frees its buffer as soon as this callback returns.
        ReadOnlySpan<char> passwordSpan = default;
        // capture the return value of the span-based overload
        var result = Import(certPath: certPath, password: passwordSpan, privateKeyPath: privateKeyPath, flags: flags);
        return result;
    }

    /// <summary>
    /// Imports an X509 certificate from the specified file path.
    /// </summary>
    /// <param name="certPath">The path to the certificate file.</param>
    /// <returns>The imported X509Certificate2 instance.</returns>
    public static X509Certificate2 Import(string certPath)
    {
        // ToSecureSpan zero-frees its buffer as soon as this callback returns.
        ReadOnlySpan<char> passwordSpan = default;
        // capture the return value of the span-based overload
        var result = Import(certPath: certPath, password: passwordSpan);
        return result;
    }



    #endregion

    #region Export
    /// <summary>
    /// Exports the specified X509 certificate to a file in the given format, with optional password and private key inclusion.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to export.</param>
    /// <param name="filePath">The file path to export the certificate to.</param>
    /// <param name="fmt">The export format (Pfx or Pem).</param>
    /// <param name="password">The password to protect the exported certificate or private key, if applicable.</param>
    /// <param name="includePrivateKey">Whether to include the private key in the export.</param>
    public static void Export(X509Certificate2 cert, string filePath, ExportFormat fmt,
           ReadOnlySpan<char> password = default, bool includePrivateKey = false)
    {
        // Normalize/validate target path and format
        filePath = NormalizeExportPath(filePath, fmt);

        // Ensure output directory exists
        EnsureOutputDirectoryExists(filePath);

        // Prepare password shapes once
        using var shapes = CreatePasswordShapes(password);

        switch (fmt)
        {
            case ExportFormat.Pfx:
                ExportPfx(cert, filePath, shapes.Secure);
                break;
            case ExportFormat.Pem:
                ExportPem(cert, filePath, password, includePrivateKey);
                break;
            default:
                throw new NotSupportedException($"Unsupported export format: {fmt}");
        }
    }

    /// <summary>
    /// Normalizes the export file path based on the desired export format.
    /// </summary>
    /// <param name="filePath">The original file path.</param>
    /// <param name="fmt">The desired export format.</param>
    /// <returns>The normalized file path.</returns>
    private static string NormalizeExportPath(string filePath, ExportFormat fmt)
    {
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
        switch (fileExtension)
        {
            case ".pfx":
                if (fmt != ExportFormat.Pfx)
                {
                    throw new NotSupportedException(
                            $"File extension '{fileExtension}' for '{filePath}' is not supported for PFX certificates.");
                }

                break;
            case ".pem":
                if (fmt != ExportFormat.Pem)
                {
                    throw new NotSupportedException(
                            $"File extension '{fileExtension}' for '{filePath}' is not supported for PEM certificates.");
                }

                break;
            case "":
                // no extension, use the format as the extension
                filePath += fmt == ExportFormat.Pfx ? ".pfx" : ".pem";
                break;
            default:
                throw new NotSupportedException(
                    $"File extension '{fileExtension}' for '{filePath}' is not supported. Use .pfx or .pem.");
        }
        return filePath;
    }

    /// <summary>
    /// Ensures the output directory exists for the specified file path.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    private static void EnsureOutputDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException(
                    $"Directory '{dir}' does not exist. Cannot export certificate to {filePath}.");
        }
    }

    /// <summary>
    /// Represents the password shapes used for exporting certificates.
    /// </summary>
    private sealed class PasswordShapes(SecureString? secure, char[]? chars) : IDisposable
    {
        public SecureString? Secure { get; } = secure;
        public char[]? Chars { get; } = chars;

        public void Dispose()
        {
            try
            {
                Secure?.Dispose();
            }
            finally
            {
                if (Chars is not null)
                {
                    Array.Clear(Chars, 0, Chars.Length);
                }
            }
        }
    }

    /// <summary>
    /// Creates password shapes from the provided password span.
    /// </summary>
    /// <param name="password">The password span.</param>
    /// <returns>The created password shapes.</returns>
    private static PasswordShapes CreatePasswordShapes(ReadOnlySpan<char> password)
    {
        var secure = password.IsEmpty ? null : SecureStringUtils.ToSecureString(password);
        var chars = password.IsEmpty ? null : password.ToArray();
        return new PasswordShapes(secure, chars);
    }

    /// <summary>
    /// Exports the specified X509 certificate to a file in the given format.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to export.</param>
    /// <param name="filePath">The file path to export the certificate to.</param>
    /// <param name="password">The SecureString password to protect the exported certificate.</param>
    private static void ExportPfx(X509Certificate2 cert, string filePath, SecureString? password)
    {
        var pfx = cert.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(filePath, pfx);
    }

    /// <summary>
    /// Exports the specified X509 certificate to a file in the given format.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to export.</param>
    /// <param name="filePath">The file path to export the certificate to.</param>
    /// <param name="password">The SecureString password to protect the exported certificate.</param>
    /// <param name="includePrivateKey">Whether to include the private key in the export.</param>
    private static void ExportPem(X509Certificate2 cert, string filePath, ReadOnlySpan<char> password, bool includePrivateKey)
    {
        // Write certificate first, then dispose writer before optional key append to avoid file locks on Windows
        using (var sw = new StreamWriter(filePath, false, Encoding.ASCII))
        {
            new PemWriter(sw).WriteObject(DotNetUtilities.FromX509Certificate(cert));
        }

        if (includePrivateKey)
        {
            WritePrivateKey(cert, password, filePath);
            // Fallback safeguard: if append was requested but key block missing, try again
            try
            {
                if (ShouldAppendKeyToPem && !File.ReadAllText(filePath).Contains("PRIVATE KEY", StringComparison.Ordinal))
                {
                    var baseName = Path.GetFileNameWithoutExtension(filePath);
                    var dir = Path.GetDirectoryName(filePath);
                    var keyFile = string.IsNullOrEmpty(dir) ? baseName + ".key" : Path.Combine(dir, baseName + ".key");
                    if (File.Exists(keyFile))
                    {
                        File.AppendAllText(filePath, Environment.NewLine + File.ReadAllText(keyFile));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Fallback attempt to append private key to PEM failed");
            }
        }
    }

    /// <summary>
    /// Writes the private key of the specified X509 certificate to a file.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to export.</param>
    /// <param name="password">The SecureString password to protect the exported private key.</param>
    /// <param name="certFilePath">The file path to export the certificate to.</param>
    private static void WritePrivateKey(X509Certificate2 cert, ReadOnlySpan<char> password, string certFilePath)
    {
        if (!cert.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "Certificate does not contain a private key; cannot export private key PEM.");
        }

        AsymmetricAlgorithm key;

        try
        {
            // Try RSA first, then ECDSA
            key = (AsymmetricAlgorithm?)cert.GetRSAPrivateKey()
                  ?? cert.GetECDsaPrivateKey()
                  ?? throw new NotSupportedException(
                        "Certificate private key is neither RSA nor ECDSA, or is not accessible.");
        }
        catch (CryptographicException ex) when (ex.HResult == unchecked((int)0x80090016))
        {
            // 0x80090016 = NTE_BAD_KEYSET  → "Keyset does not exist"
            throw new InvalidOperationException(
                "The certificate reports a private key, but the key container ('keyset') is not accessible. " +
                "This usually means the certificate was loaded without its private key, or the current process " +
                "identity does not have permission to access the key. Re-import the certificate from a PFX " +
                "with the private key and X509KeyStorageFlags.Exportable, or adjust key permissions.",
                ex);
        }

        byte[] keyDer;
        string pemLabel;

        if (password.IsEmpty)
        {
            // unencrypted PKCS#8
            keyDer = key switch
            {
                RSA rsa => rsa.ExportPkcs8PrivateKey(),
                ECDsa ecc => ecc.ExportPkcs8PrivateKey(),
                _ => throw new NotSupportedException("Only RSA and ECDSA private keys are supported.")
            };
            pemLabel = "PRIVATE KEY";
        }
        else
        {
            // encrypted PKCS#8
            var pbe = new PbeParameters(
                PbeEncryptionAlgorithm.Aes256Cbc,
                HashAlgorithmName.SHA256,
                iterationCount: 100_000);

            keyDer = key switch
            {
                RSA rsa => rsa.ExportEncryptedPkcs8PrivateKey(password, pbe),
                ECDsa ecc => ecc.ExportEncryptedPkcs8PrivateKey(password, pbe),
                _ => throw new NotSupportedException("Only RSA and ECDSA private keys are supported.")
            };
            pemLabel = "ENCRYPTED PRIVATE KEY";
        }

        var keyPem = PemEncoding.WriteString(pemLabel, keyDer);
        var certDir = Path.GetDirectoryName(certFilePath);
        var baseName = Path.GetFileNameWithoutExtension(certFilePath);
        var keyFilePath = string.IsNullOrEmpty(certDir)
            ? baseName + ".key"
            : Path.Combine(certDir, baseName + ".key");

        File.WriteAllText(keyFilePath, keyPem);

        try
        {
            if (ShouldAppendKeyToPem)
            {
                File.AppendAllText(certFilePath, Environment.NewLine + keyPem);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex,
                "Failed to append private key to certificate PEM file {CertFilePath}; continuing with separate key file only",
                certFilePath);
        }
    }


    /// <summary>
    /// Exports the specified X509 certificate to a file in the given format, using a SecureString password and optional private key inclusion.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to export.</param>
    /// <param name="filePath">The file path to export the certificate to.</param>
    /// <param name="fmt">The export format (Pfx or Pem).</param>
    /// <param name="password">The SecureString password to protect the exported certificate or private key, if applicable.</param>
    /// <param name="includePrivateKey">Whether to include the private key in the export.</param>
    public static void Export(
        X509Certificate2 cert,
        string filePath,
        ExportFormat fmt,
        SecureString password,
        bool includePrivateKey = false)
    {
        if (password is null)
        {
            // Delegate to span-based overload with no password
            Export(cert, filePath, fmt, [], includePrivateKey);
        }
        else
        {
            password.ToSecureSpan(span =>
                Export(cert, filePath, fmt, span, includePrivateKey)
            // this will run your span‐based implementation,
            // then immediately zero & free the unmanaged buffer
            );
        }
    }


    /// <summary>
    /// Creates a self-signed certificate from the given RSA JWK JSON and exports it
    /// as a PEM certificate (optionally including the private key) to the specified path.
    /// </summary>
    /// <param name="jwkJson">The RSA JWK JSON string.</param>
    /// <param name="filePath">
    /// Target file path. If no extension is provided, ".pem" will be added.
    /// </param>
    /// <param name="password">
    /// Optional password used to encrypt the private key when <paramref name="includePrivateKey"/> is true.
    /// Ignored when <paramref name="includePrivateKey"/> is false.
    /// </param>
    /// <param name="includePrivateKey">
    /// If true, the PEM export will include the private key (and create a .key file as per Export logic).
    /// </param>
    public static void ExportPemFromJwkJson(
        string jwkJson,
        string filePath,
        ReadOnlySpan<char> password = default,
        bool includePrivateKey = false)
    {
        if (string.IsNullOrWhiteSpace(jwkJson))
        {
            throw new ArgumentException("JWK JSON cannot be null or empty.", nameof(jwkJson));
        }

        // 1) Create a self-signed certificate from the JWK
        var cert = CreateSelfSignedCertificateFromJwk(jwkJson);

        // 2) Reuse the existing Export pipeline to write PEM (cert + optional key)
        Export(cert, filePath, ExportFormat.Pem, password, includePrivateKey);
    }

    /// <summary>
    /// Creates a self-signed certificate from the given RSA JWK JSON and exports it
    /// as a PEM certificate (optionally including the private key) to the specified path,
    /// using a <see cref="SecureString"/> password.
    /// </summary>
    /// <param name="jwkJson">The RSA JWK JSON string.</param>
    /// <param name="filePath">Target file path for the PEM output.</param>
    /// <param name="password">
    /// SecureString password used to encrypt the private key when
    /// <paramref name="includePrivateKey"/> is true.
    /// </param>
    /// <param name="includePrivateKey">
    /// If true, the PEM export will include the private key.
    /// </param>
    public static void ExportPemFromJwkJson(
        string jwkJson,
        string filePath,
        SecureString password,
        bool includePrivateKey = false)
    {
        if (password is null)
        {
            // Delegate to span-based overload with no password
            ExportPemFromJwkJson(jwkJson, filePath, [], includePrivateKey);
            return;
        }

        password.ToSecureSpan(span =>
        {
            ExportPemFromJwkJson(jwkJson, filePath, span, includePrivateKey);
        });
    }


    #endregion

    #region JWK


    private static readonly JsonSerializerOptions s_jwkJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Creates a self-signed X509 certificate from the provided RSA JWK JSON string.
    /// </summary>
    /// <param name="jwkJson">The JSON string representing the RSA JWK.</param>
    /// <param name="subjectName">The subject name for the certificate.</param>
    /// <returns>A self-signed X509Certificate2 instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the JWK JSON is invalid.</exception>
    /// <exception cref="NotSupportedException"></exception>
    public static X509Certificate2 CreateSelfSignedCertificateFromJwk(
        string jwkJson,
        string subjectName = "CN=client-jwt")
    {
        var jwk = JsonSerializer.Deserialize<RsaJwk>(jwkJson)
                  ?? throw new ArgumentException("Invalid JWK JSON");

        if (!string.Equals(jwk.Kty, "RSA", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only RSA JWKs are supported.");
        }

        var rsaParams = new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(jwk.N),
            Exponent = Base64UrlEncoder.DecodeBytes(jwk.E),
            D = Base64UrlEncoder.DecodeBytes(jwk.D),
            P = Base64UrlEncoder.DecodeBytes(jwk.P),
            Q = Base64UrlEncoder.DecodeBytes(jwk.Q),
            DP = Base64UrlEncoder.DecodeBytes(jwk.DP),
            DQ = Base64UrlEncoder.DecodeBytes(jwk.DQ),
            InverseQ = Base64UrlEncoder.DecodeBytes(jwk.QI)
        };

        using var rsa = RSA.Create();
        rsa.ImportParameters(rsaParams);

        var req = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Self-signed, 1 year validity (tune as you like)
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = notBefore.AddYears(1);

        var cert = req.CreateSelfSigned(notBefore, notAfter);

        // Export with private key, re-import as X509Certificate2
        var pfxBytes = cert.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            password: default,
            keyStorageFlags: X509KeyStorageFlags.Exportable,
            loaderLimits: Pkcs12LoaderLimits.Defaults);
#else
        return new X509Certificate2(pfxBytes, (string?)null,
            X509KeyStorageFlags.Exportable);
#endif
    }

    /// <summary>
    /// Builds a Private Key JWT for client authentication using the specified certificate.
    /// </summary>
    /// <param name="key">The security key (X509SecurityKey or JsonWebKey) to sign the JWT.</param>
    /// <param name="clientId">The client ID (issuer and subject) for the JWT.</param>
    /// <param name="tokenEndpoint">The token endpoint URL (audience) for the JWT.</param>
    /// <returns>The generated Private Key JWT as a string.</returns>
    public static string BuildPrivateKeyJwt(
        SecurityKey key,
        string clientId,
        string tokenEndpoint)
    {
        var now = DateTimeOffset.UtcNow;

        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var handler = new JsonWebTokenHandler();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = clientId,
            Audience = tokenEndpoint,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", clientId),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ]),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(2).UtcDateTime,
            SigningCredentials = creds
        };

        return handler.CreateToken(descriptor);
    }

    /// <summary>
    /// Builds a Private Key JWT for client authentication using the specified X509 certificate.
    /// </summary>
    /// <param name="certificate">The X509 certificate containing the private key.</param>
    /// <param name="clientId">The client ID (issuer and subject) for the JWT.</param>
    /// <param name="tokenEndpoint">The token endpoint URL (audience) for the JWT.</param>
    /// <returns>The generated Private Key JWT as a string.</returns>
    public static string BuildPrivateKeyJwt(
        X509Certificate2 certificate,
        string clientId,
        string tokenEndpoint)
    {
        var key = new X509SecurityKey(certificate)
        {
            KeyId = certificate.Thumbprint
        };

        return BuildPrivateKeyJwt(key, clientId, tokenEndpoint);
    }

    /// <summary>
    /// Builds a Private Key JWT for client authentication using the specified JWK JSON string.
    /// </summary>
    /// <param name="jwkJson">The JWK JSON string representing the key.</param>
    /// <param name="clientId">The client ID (issuer and subject) for the JWT.</param>
    /// <param name="tokenEndpoint">The token endpoint URL (audience) for the JWT.</param>
    /// <returns>The generated Private Key JWT as a string.</returns>
    public static string BuildPrivateKeyJwtFromJwkJson(
        string jwkJson,
        string clientId,
        string tokenEndpoint)
    {
        var jwk = new JsonWebKey(jwkJson);
        // You can set KeyId here if you want to use kid from the JSON:
        // jwk.KeyId is automatically populated from "kid" if present.

        return BuildPrivateKeyJwt(jwk, clientId, tokenEndpoint);
    }


    /// <summary>
    /// Builds a JWK JSON (RSA) representation of the given certificate.
    /// By default only public parameters are included (safe for publishing as JWKS).
    /// Set <paramref name="includePrivateParameters"/> to true if you want a full private JWK
    /// (for local storage only – never publish it).
    /// </summary>
    /// <param name="certificate">The X509 certificate to convert.</param>
    /// <param name="includePrivateParameters">Whether to include private key parameters in the JWK.</param>
    /// <returns>The JWK JSON string.</returns>
    public static string CreateJwkJsonFromCertificate(
       X509Certificate2 certificate,
       bool includePrivateParameters = false)
    {
        var x509Key = new X509SecurityKey(certificate)
        {
            KeyId = certificate.Thumbprint?.ToLowerInvariant()
        };

        // Convert to a JsonWebKey (n, e, kid, x5c, etc.)
        var jwk = JsonWebKeyConverter.ConvertFromX509SecurityKey(
            x509Key,
            representAsRsaKey: true);

        if (!includePrivateParameters)
        {
            // Clean public JWK
            jwk.D = null;
            jwk.P = null;
            jwk.Q = null;
            jwk.DP = null;
            jwk.DQ = null;
            jwk.QI = null;
        }
        else
        {
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("Certificate has no private key.");
            }

            using var rsa = certificate.GetRSAPrivateKey()
                ?? throw new NotSupportedException("Certificate does not contain an RSA private key.");

            var p = rsa.ExportParameters(true);

            jwk.N = Base64UrlEncoder.Encode(p.Modulus);
            jwk.E = Base64UrlEncoder.Encode(p.Exponent);
            jwk.D = Base64UrlEncoder.Encode(p.D);
            jwk.P = Base64UrlEncoder.Encode(p.P);
            jwk.Q = Base64UrlEncoder.Encode(p.Q);
            jwk.DP = Base64UrlEncoder.Encode(p.DP);
            jwk.DQ = Base64UrlEncoder.Encode(p.DQ);
            jwk.QI = Base64UrlEncoder.Encode(p.InverseQ);
        }

        return JsonSerializer.Serialize(jwk, s_jwkJsonOptions);
    }

    /// <summary>
    /// Creates an RSA JWK JSON from a given RSA instance (must contain private key).
    /// </summary>
    /// <param name="rsa">The RSA instance with a private key.</param>
    /// <param name="keyId">Optional key identifier (kid) to set on the JWK.</param>
    /// <returns>JWK JSON string containing public and private parameters.</returns>
    public static string CreateJwkJsonFromRsa(RSA rsa, string? keyId = null)
    {
        ArgumentNullException.ThrowIfNull(rsa);

        // true => includes private key params (d, p, q, dp, dq, qi)
        var p = rsa.ExportParameters(includePrivateParameters: true);

        if (p.D is null || p.P is null || p.Q is null ||
            p.DP is null || p.DQ is null || p.InverseQ is null)
        {
            throw new InvalidOperationException("RSA key does not contain private parameters.");
        }

        var jwk = new RsaJwk
        {
            Kty = "RSA",
            N = Base64UrlEncoder.Encode(p.Modulus),
            E = Base64UrlEncoder.Encode(p.Exponent),
            D = Base64UrlEncoder.Encode(p.D),
            P = Base64UrlEncoder.Encode(p.P),
            Q = Base64UrlEncoder.Encode(p.Q),
            DP = Base64UrlEncoder.Encode(p.DP),
            DQ = Base64UrlEncoder.Encode(p.DQ),
            QI = Base64UrlEncoder.Encode(p.InverseQ),
            Kid = keyId
        };

        return JsonSerializer.Serialize(jwk, s_jwkJsonOptions);
    }

    /// <summary>
    /// Creates an RSA JWK JSON from a PKCS#1 or PKCS#8 RSA private key in PEM format.
    /// </summary>
    /// <param name="rsaPrivateKeyPem">
    /// PEM containing an RSA private key (e.g. "-----BEGIN RSA PRIVATE KEY----- ...").
    /// </param>
    /// <param name="keyId">Optional key identifier (kid) to set on the JWK.</param>
    /// <returns>JWK JSON string containing public and private parameters.</returns>
    public static string CreateJwkJsonFromRsaPrivateKeyPem(
        string rsaPrivateKeyPem,
        string? keyId = null)
    {
        if (string.IsNullOrWhiteSpace(rsaPrivateKeyPem))
        {
            throw new ArgumentException("RSA private key PEM cannot be null or empty.", nameof(rsaPrivateKeyPem));
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(rsaPrivateKeyPem.AsSpan());

        return CreateJwkJsonFromRsa(rsa, keyId);
    }



    #endregion

    #region  Validation helpers (Test-PodeCertificate equivalent)
    /// <summary>
    /// Validates the specified X509 certificate according to the provided options.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to validate.</param>
    /// <param name="checkRevocation">Whether to check certificate revocation status.</param>
    /// <param name="allowWeakAlgorithms">Whether to allow weak algorithms such as SHA-1 or small key sizes.</param>
    /// <param name="denySelfSigned">Whether to deny self-signed certificates.</param>
    /// <param name="expectedPurpose">A collection of expected key purposes (EKU) for the certificate.</param>
    /// <param name="strictPurpose">If true, the certificate must match the expected purposes exactly.</param>
    /// <returns>True if the certificate is valid according to the specified options; otherwise, false.</returns>
    public static bool Validate(
     X509Certificate2 cert,
     bool checkRevocation = false,
     bool allowWeakAlgorithms = false,
     bool denySelfSigned = false,
     OidCollection? expectedPurpose = null,
     bool strictPurpose = false)
    {
        // 1) Validity period
        if (!IsWithinValidityPeriod(cert))
        {
            return false;
        }

        // 2) Self-signed policy
        var isSelfSigned = cert.Subject == cert.Issuer;
        if (denySelfSigned && isSelfSigned)
        {
            return false;
        }

        // Pre-compute weakness so we can apply it consistently across validation steps.
        var isWeak = UsesWeakAlgorithms(cert);

        // 3) Chain build (with optional revocation)
        if (!BuildChainOk(cert, checkRevocation, isSelfSigned, allowWeakAlgorithms, isWeak))
        {
            return false;
        }

        // 4) EKU / purposes
        if (!PurposesOk(cert, expectedPurpose, strictPurpose))
        {
            return false;
        }

        // 5) Weak algorithms
        if (!allowWeakAlgorithms && isWeak)
        {
            return false;
        }

        return true;   // ✅ everything passed
    }

    /// <summary>
    /// Checks if the certificate is within its validity period.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to check.</param>
    /// <returns>True if the certificate is within its validity period; otherwise, false.</returns>
    private static bool IsWithinValidityPeriod(X509Certificate2 cert)
    {
        var notBeforeUtc = cert.NotBefore.Kind == DateTimeKind.Utc
            ? cert.NotBefore
            : cert.NotBefore.ToUniversalTime();

        var notAfterUtc = cert.NotAfter.Kind == DateTimeKind.Utc
            ? cert.NotAfter
            : cert.NotAfter.ToUniversalTime();

        var nowUtc = DateTime.UtcNow;
        return nowUtc >= notBeforeUtc && nowUtc <= notAfterUtc;
    }

    /// <summary>
    /// Checks if the certificate chain is valid.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to check.</param>
    /// <param name="checkRevocation">Whether to check certificate revocation status.</param>
    /// <param name="isSelfSigned">Whether the certificate is self-signed.</param>
    /// <returns>True if the certificate chain is valid; otherwise, false.</returns>
    private static bool BuildChainOk(X509Certificate2 cert, bool checkRevocation, bool isSelfSigned)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = checkRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EndCertificateOnly;
        chain.ChainPolicy.DisableCertificateDownloads = !checkRevocation;

        if (isSelfSigned)
        {
            // Make self-signed validation deterministic across platforms.
            // Using the platform trust store differs between Windows/macOS/Linux; custom root trust
            // avoids false negatives for dev/self-signed certificates.
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            _ = chain.ChainPolicy.CustomTrustStore.Add(cert);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        }

        var ok = chain.Build(cert);
        if (ok)
        {
            return true;
        }

        if (!isSelfSigned)
        {
            return false;
        }

        // Some platforms still report non-fatal statuses for self-signed roots.
        // Treat these as acceptable for self-signed certificates.
        var allowed = X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain;
        if (!checkRevocation)
        {
            allowed |= X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
        }

        var combined = X509ChainStatusFlags.NoError;
        foreach (var status in chain.ChainStatus)
        {
            combined |= status.Status;
        }

        return (combined & ~allowed) == 0;
    }

    /// <summary>
    /// Checks if the certificate chain is valid.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to check.</param>
    /// <param name="checkRevocation">Whether to check certificate revocation status.</param>
    /// <param name="isSelfSigned">Whether the certificate is self-signed.</param>
    /// <param name="allowWeakAlgorithms">Whether weak algorithms are allowed.</param>
    /// <param name="isWeak">Whether the certificate is considered weak by this library.</param>
    /// <returns>True if the certificate chain is valid; otherwise, false.</returns>
    private static bool BuildChainOk(
        X509Certificate2 cert,
        bool checkRevocation,
        bool isSelfSigned,
        bool allowWeakAlgorithms,
        bool isWeak) => isSelfSigned && allowWeakAlgorithms && isWeak || BuildChainOk(cert, checkRevocation, isSelfSigned);

    /// <summary>
    /// Checks if the certificate has the expected key purposes (EKU).
    /// </summary>
    /// <param name="cert">The X509Certificate2 to check.</param>
    /// <param name="expectedPurpose">A collection of expected key purposes (EKU) for the certificate.</param>
    /// <param name="strictPurpose">If true, the certificate must match the expected purposes exactly.</param>
    /// <returns>True if the certificate has the expected purposes; otherwise, false.</returns>
    private static bool PurposesOk(X509Certificate2 cert, OidCollection? expectedPurpose, bool strictPurpose)
    {
        if (expectedPurpose is not { Count: > 0 })
        {
            return true; // nothing to check
        }

        var eku = GetEkuOids(cert);
        var wanted = expectedPurpose
            .Cast<Oid>()
            .Select(static o => o.Value)
            .Where(static v => !string.IsNullOrWhiteSpace(v))
            .Select(static v => v!)
            .ToHashSet(StringComparer.Ordinal);

        return wanted.Count == 0 || eku.Count != 0 && (strictPurpose ? eku.SetEquals(wanted) : wanted.IsSubsetOf(eku));
    }

    /// <summary>
    /// Extracts EKU OIDs from the certificate, robustly across platforms.
    /// </summary>
    /// <param name="cert">The certificate to inspect.</param>
    /// <returns>A set of EKU OID strings.</returns>
    private static HashSet<string> GetEkuOids(X509Certificate2 cert)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        // EKU extension OID
        var ext = cert.Extensions["2.5.29.37"];
        if (ext == null)
        {
            return set;
        }

        var ekuExt = ext as X509EnhancedKeyUsageExtension
            ?? new X509EnhancedKeyUsageExtension(ext, ext.Critical);

        foreach (var oid in ekuExt.EnhancedKeyUsages.Cast<Oid>())
        {
            if (!string.IsNullOrWhiteSpace(oid.Value))
            {
                _ = set.Add(oid.Value);
            }
        }

        return set;
    }

    /// <summary>
    /// Checks if the certificate uses weak algorithms.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to check.</param>
    /// <returns>True if the certificate uses weak algorithms; otherwise, false.</returns>
    private static bool UsesWeakAlgorithms(X509Certificate2 cert)
    {
        var isSha1 = cert.SignatureAlgorithm?.FriendlyName?
                           .Contains("sha1", StringComparison.OrdinalIgnoreCase) == true;

        var weakRsa = cert.GetRSAPublicKey() is { KeySize: < 2048 };
        var weakDsa = cert.GetDSAPublicKey() is { KeySize: < 2048 };
        var weakEcdsa = cert.GetECDsaPublicKey() is { KeySize: < 256 };  // P-256 minimum

        return isSha1 || weakRsa || weakDsa || weakEcdsa;
    }


    /// <summary>
    /// Gets the enhanced key usage purposes (EKU) from the specified X509 certificate.
    /// </summary>
    /// <param name="cert">The X509Certificate2 to extract purposes from.</param>
    /// <returns>An enumerable of purpose names or OID values.</returns>
    public static IEnumerable<string> GetPurposes(X509Certificate2 cert) =>
        cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(x => x.EnhancedKeyUsages.Cast<Oid>())
            .Select(o => (o.FriendlyName ?? o.Value)!)   // ← null-forgiving
            .Where(s => s.Length > 0);                   // optional: drop empties
    #endregion

    #region  private helpers
    private static AsymmetricCipherKeyPair GenRsaKeyPair(int bits, SecureRandom rng)
    {
        var gen = new RsaKeyPairGenerator();
        gen.Init(new KeyGenerationParameters(rng, bits));
        return gen.GenerateKeyPair();
    }

    /// <summary>
    /// Generates an EC key pair.
    /// </summary>
    /// <param name="bits">The key size in bits.</param>
    /// <param name="rng">The secure random number generator.</param>
    /// <returns>The generated EC key pair.</returns>
    private static AsymmetricCipherKeyPair GenEcKeyPair(int bits, SecureRandom rng)
    {
        // NIST-style names are fine here
        var name = bits switch
        {
            <= 256 => "P-256",
            <= 384 => "P-384",
            _ => "P-521"
        };

        // ECNamedCurveTable knows about SEC *and* NIST names
        var ecParams = ECNamedCurveTable.GetByName(name)
                       ?? throw new InvalidOperationException($"Curve not found: {name}");

        var domain = new ECDomainParameters(
            ecParams.Curve, ecParams.G, ecParams.N, ecParams.H, ecParams.GetSeed());

        var gen = new ECKeyPairGenerator();
        gen.Init(new ECKeyGenerationParameters(domain, rng));
        return gen.GenerateKeyPair();
    }

    /// <summary>
    /// Converts a BouncyCastle X509Certificate to a .NET X509Certificate2.
    /// </summary>
    /// <param name="cert">The BouncyCastle X509Certificate to convert.</param>
    /// <param name="privKey">The private key associated with the certificate.</param>
    /// <param name="flags">The key storage flags to use.</param>
    /// <param name="ephemeral">Whether the key is ephemeral.</param>
    /// <returns></returns>
    private static X509Certificate2 ToX509Cert2(
        Org.BouncyCastle.X509.X509Certificate cert,
        AsymmetricKeyParameter privKey,
        X509KeyStorageFlags flags,
        bool ephemeral)
    {
        var store = new Pkcs12StoreBuilder().Build();
        var entry = new X509CertificateEntry(cert);
        const string alias = "cert";
        store.SetCertificateEntry(alias, entry);
        store.SetKeyEntry(alias, new AsymmetricKeyEntry(privKey),
                          [entry]);

        using var ms = new MemoryStream();
        store.Save(ms, [], new SecureRandom());
        var raw = ms.ToArray();

#if NET9_0_OR_GREATER
        try
        {
            return X509CertificateLoader.LoadPkcs12(
                raw,
                password: default,
                keyStorageFlags: flags | (ephemeral ? X509KeyStorageFlags.EphemeralKeySet : 0),
                loaderLimits: Pkcs12LoaderLimits.Defaults
            );
        }
        catch (PlatformNotSupportedException) when (ephemeral)
        {
            // Some platforms (e.g. certain Linux/macOS runners) don't yet support
            // EphemeralKeySet with the new X509CertificateLoader API. In that case
            // we fall back to re-loading without the EphemeralKeySet flag. The
            // intent of Ephemeral in our API is simply "do not persist in a store" –
            // loading without the flag here still keeps the cert in-memory only.
            Log.Debug("EphemeralKeySet not supported on this platform for X509CertificateLoader; falling back without the flag.");
            return X509CertificateLoader.LoadPkcs12(
                raw,
                password: default,
                keyStorageFlags: flags, // omit EphemeralKeySet
                loaderLimits: Pkcs12LoaderLimits.Defaults
            );
        }
#else
        try
        {
            return new X509Certificate2(
                raw,
                (string?)null,
                flags | (ephemeral ? X509KeyStorageFlags.EphemeralKeySet : 0)
            );
        }
        catch (PlatformNotSupportedException) when (ephemeral)
        {
            // macOS (and some Linux distros) under net8 may not support EphemeralKeySet here.
            Log.Debug("EphemeralKeySet not supported on this platform (net8); falling back without the flag.");
            return new X509Certificate2(
                raw,
                (string?)null,
                flags // omit EphemeralKeySet
            );
        }

#endif
    }

    #endregion
}
