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
    #region  enums / option records
    /// <summary>
    /// Specifies the type of cryptographic key to use for certificate operations.
    /// </summary>
    /// <summary>
    /// Specifies the cryptographic key type.
    /// </summary>
    public enum KeyType
    {
        /// <summary>
        /// RSA key type.
        /// </summary>
        Rsa,
        /// <summary>
        /// ECDSA key type.
        /// </summary>
        Ecdsa
    }

    /// <summary>
    /// Specifies the format to use when exporting certificates.
    /// </summary>
    /// <summary>
    /// Specifies the format to use when exporting certificates.
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>
        /// PFX/PKCS#12 format.
        /// </summary>
        Pfx,
        /// <summary>
        /// PEM format.
        /// </summary>
        Pem
    }

    /// <summary>
    /// Options for creating a self-signed certificate.
    /// </summary>
    /// <param name="DnsNames">The DNS names to include in the certificate's Subject Alternative Name (SAN) extension.</param>
    /// <param name="KeyType">The type of cryptographic key to use (RSA or ECDSA).</param>
    /// <param name="KeyLength">The length of the cryptographic key in bits.</param>
    /// <param name="Purposes">The key purposes (Extended Key Usage) for the certificate.</param>
    /// <param name="ValidDays">The number of days the certificate will be valid.</param>
    /// <param name="Ephemeral">If true, the certificate will not be stored in the Windows certificate store.</param>
    /// <param name="Exportable">If true, the private key can be exported from the certificate.</param>
    /// <remarks>
    /// This record is used to specify options for creating a self-signed certificate.
    /// </remarks>
    public record SelfSignedOptions(
        IEnumerable<string> DnsNames,
        KeyType KeyType = KeyType.Rsa,
        int KeyLength = 2048,
        IEnumerable<KeyPurposeID>? Purposes = null,
        int ValidDays = 365,
        bool Ephemeral = false,
        bool Exportable = false
        );

    /// <summary>
    /// Options for creating a Certificate Signing Request (CSR).
    /// </summary>
    /// <param name="DnsNames">The DNS names to include in the CSR's Subject Alternative Name (SAN) extension.</param>
    /// <param name="KeyType">The type of cryptographic key to use (RSA or ECDSA).</param>
    /// <param name="KeyLength">The length of the cryptographic key in bits.</param>
    /// <param name="Country">The country code for the subject distinguished name.</param>
    /// <param name="Org">The organization name for the subject distinguished name.</param>
    /// <param name="OrgUnit">The organizational unit for the subject distinguished name.</param>
    /// <param name="CommonName">The common name for the subject distinguished name.</param>
    /// <remarks>
    /// This record is used to specify options for creating a Certificate Signing Request (CSR).
    /// </remarks>
    public record CsrOptions(
        IEnumerable<string> DnsNames,
        KeyType KeyType = KeyType.Rsa,
        int KeyLength = 2048,
        string? Country = null,
        string? Org = null,
        string? OrgUnit = null,
        string? CommonName = null);
    #endregion

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
            if (!string.IsNullOrWhiteSpace(v)) { order.Add(oid); attrs[oid] = v!; }
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

            if (encDer is null)
            {
                Log.Debug("Encrypted PEM manual pairing fallback skipped: markers not found in key file {KeyFile}", privateKeyPath);
                return;
            }

            Exception? lastErr = null;
            for (var round = 0; round < 2; round++)
            {
                // Try RSA
                try
                {
                    using var rsa = RSA.Create();
                    rsa.ImportEncryptedPkcs8PrivateKey(password, encDer, out _);
                    var withKey = certOnly.CopyWithPrivateKey(rsa);
                    if (withKey.HasPrivateKey)
                    {
                        Log.Debug("Encrypted PEM manual pairing succeeded with RSA private key (round {Round}).", round + 1);
                        loaded = withKey;
                        return;
                    }
                }
                catch (Exception exRsa)
                {
                    lastErr = lastErr is null ? exRsa : new AggregateException(lastErr, exRsa);
                }

                // Try ECDSA
                try
                {
                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportEncryptedPkcs8PrivateKey(password, encDer, out _);
                    var withKey = certOnly.CopyWithPrivateKey(ecdsa);
                    if (withKey.HasPrivateKey)
                    {
                        Log.Debug("Encrypted PEM manual pairing succeeded with ECDSA private key (round {Round}).", round + 1);
                        loaded = withKey;
                        return;
                    }
                }
                catch (Exception exEc)
                {
                    lastErr = lastErr is null ? exEc : new AggregateException(lastErr, exEc);
                }
                Thread.Sleep(25 * (round + 1));
            }

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
        return result!;
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
        return result!;
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
                    var keyFile = string.IsNullOrEmpty(dir) ? baseName + ".key" : Path.Combine(dir!, baseName + ".key");
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
        byte[] keyDer;
        string pemLabel;
        if (password.IsEmpty)
        {
            // unencrypted PKCS#8
            keyDer = cert.GetRSAPrivateKey() is RSA rsa
                       ? rsa.ExportPkcs8PrivateKey()
                       : cert.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
            pemLabel = "PRIVATE KEY";
        }
        else
        {
            // encrypted PKCS#8
            var pbe = new PbeParameters(
                PbeEncryptionAlgorithm.Aes256Cbc,
                HashAlgorithmName.SHA256,
                100_000
            );

            keyDer = cert.GetRSAPrivateKey() is RSA rsaEnc
                       ? rsaEnc.ExportEncryptedPkcs8PrivateKey(password, pbe)
                       : cert.GetECDsaPrivateKey()!.ExportEncryptedPkcs8PrivateKey(password, pbe);
            pemLabel = "ENCRYPTED PRIVATE KEY";
        }

        var keyPem = PemEncoding.WriteString(pemLabel, keyDer);
        var certDir = Path.GetDirectoryName(certFilePath);
        var baseName = Path.GetFileNameWithoutExtension(certFilePath);
        var keyFilePath = string.IsNullOrEmpty(certDir)
            ? baseName + ".key"
            : Path.Combine(certDir!, baseName + ".key");
        File.WriteAllText(keyFilePath, keyPem);

        try
        {
            if (ShouldAppendKeyToPem)
            {
                // Optional: append the key to the main certificate PEM when explicitly enabled.
                File.AppendAllText(certFilePath, Environment.NewLine + keyPem);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to append private key to certificate PEM file {CertFilePath}; continuing with separate key file only", certFilePath);
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
        password.ToSecureSpan(span =>
            // this will run your span‐based implementation,
            // then immediately zero & free the unmanaged buffer
            Export(cert, filePath, fmt, span, includePrivateKey)
        );
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

        // 3) Chain build (with optional revocation)
        if (!BuildChainOk(cert, checkRevocation, isSelfSigned))
        {
            return false;
        }

        // 4) EKU / purposes
        if (!PurposesOk(cert, expectedPurpose, strictPurpose))
        {
            return false;
        }

        // 5) Weak algorithms
        if (!allowWeakAlgorithms && UsesWeakAlgorithms(cert))
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
        => DateTime.UtcNow >= cert.NotBefore && DateTime.UtcNow <= cert.NotAfter;

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
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        }

        return chain.Build(cert);
    }

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

        var eku = cert.Extensions
                       .OfType<X509EnhancedKeyUsageExtension>()
                       .SelectMany(e => e.EnhancedKeyUsages.Cast<Oid>())
                       .Select(o => o.Value)
                       .ToHashSet();

        var wanted = expectedPurpose.Cast<Oid>()
                                    .Select(o => o.Value)
                                    .ToHashSet();

        return strictPurpose ? eku.SetEquals(wanted) : wanted.All(eku.Contains);
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
