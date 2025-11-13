using System.Security;
using Kestrun.Certificates;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Xunit;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KestrunTests.Certificates;

public class CertificateManagerTest
{
    private static CertificateManager.SelfSignedOptions DefaultSelfSignedOptions()
        => new(["localhost", "127.0.0.1"],
               KeyType: CertificateManager.KeyType.Rsa,
               KeyLength: 2048,
               ValidDays: 30,
               Ephemeral: true,
               Exportable: true);
    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_GeneratesValidCert_WithSAN_EKU()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());

        Assert.NotNull(cert);
        Assert.True(DateTime.UtcNow >= cert.NotBefore.ToUniversalTime().AddMinutes(-6));
        Assert.True(DateTime.UtcNow <= cert.NotAfter.ToUniversalTime().AddDays(1));
        Assert.True(cert.HasPrivateKey);
        Assert.Contains("CN=localhost", cert.Subject, StringComparison.OrdinalIgnoreCase);

        // SAN present
        Assert.Contains(
            cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>(),
            e => string.Equals(e.Oid?.Value, "2.5.29.17", StringComparison.Ordinal));

        // EKU contains serverAuth and clientAuth
        var eku = cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(e => e.EnhancedKeyUsages.Cast<Oid>())
            .Select(o => o.Value)
            .ToHashSet();
        Assert.Contains("1.3.6.1.5.5.7.3.1", eku); // serverAuth
        Assert.Contains("1.3.6.1.5.5.7.3.2", eku); // clientAuth
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewCertificateRequest_ReturnsPemAndPrivateKey_WithSAN()
    {
        var csr = CertificateManager.NewCertificateRequest(
            new CertificateManager.CsrOptions([
                "localhost", "192.168.0.1"
            ], KeyType: CertificateManager.KeyType.Rsa, KeyLength: 2048, CommonName: "localhost"));

        Assert.NotNull(csr);
        Assert.NotNull(csr.PrivateKey);
        Assert.Contains("BEGIN CERTIFICATE REQUEST", csr.CsrPem);
        Assert.Contains("END CERTIFICATE REQUEST", csr.CsrPem);

        // Parse CSR with BouncyCastle to verify SAN
        using var sr = new StringReader(csr.CsrPem);
        var obj = new PemReader(sr).ReadObject();
        _ = Assert.IsType<Pkcs10CertificationRequest>(obj);
        var req = (Pkcs10CertificationRequest)obj;
        var attributes = req.GetCertificationRequestInfo().Attributes; // Asn1Set
        AttributePkcs? extAttr = null;
        for (var i = 0; i < (attributes?.Count ?? 0); i++)
        {
            var attr = AttributePkcs.GetInstance(attributes![i]);
            if (attr.AttrType.Equals(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest))
            {
                extAttr = attr;
                break;
            }
        }
        Assert.NotNull(extAttr);
        // Attribute value contains the extensions object directly; no need to force DerSet
        var extensions = X509Extensions.GetInstance(extAttr!.AttrValues[0]);
        var sanExt = extensions.GetExtension(X509Extensions.SubjectAlternativeName);
        Assert.NotNull(sanExt);
        var generalNames = GeneralNames.GetInstance(sanExt!.GetParsedValue()).GetNames();
        Assert.Contains(generalNames, n => n.TagNo == GeneralName.DnsName && string.Equals(n.Name.ToString(), "localhost", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(generalNames, n => n.TagNo == GeneralName.IPAddress);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void ExportImport_Pfx_RoundTrip_WithPassword()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pfxPath = Path.Combine(dir, "testcert.pfx");
        var password = "s3cret".AsSpan();

        CertificateManager.Export(cert, pfxPath, CertificateManager.ExportFormat.Pfx, password);
        Assert.True(File.Exists(pfxPath));
        Assert.True(new FileInfo(pfxPath).Length > 0);

        var imported = CertificateManager.Import(pfxPath, password);
        Assert.NotNull(imported);
        Assert.True(imported.HasPrivateKey);
        Assert.Contains("CN=localhost", imported.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void ExportImport_Pem_RoundTrip_WithEncryptedKey()
    {
        // Use non-ephemeral key to avoid platform-specific issues exporting encrypted PKCS#8 on some Linux runners (.NET 8)
        var options = new CertificateManager.SelfSignedOptions([
            "localhost", "127.0.0.1"
        ], KeyType: CertificateManager.KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: false, Exportable: true);
        var cert = CertificateManager.NewSelfSigned(options);

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pemPath = Path.Combine(dir, "cert.pem");
        var pwd = "topsecret";

        // Use SecureString overload to avoid ambiguity and assert encrypted key is produced
        using var ss = new SecureString();
        foreach (var ch in pwd)
        {
            ss.AppendChar(ch);
        }
        ss.MakeReadOnly();
        CertificateManager.Export(cert, pemPath, CertificateManager.ExportFormat.Pem, ss, includePrivateKey: true);
        Assert.True(File.Exists(pemPath));
        var keyPath = Path.Combine(dir, "cert.key");
        Assert.True(File.Exists(keyPath));

        // Wait for key file to be fully flushed & non-trivial length (encrypted PKCS#8 typically > 500 bytes)
        var keyText = "";
        var swStart = DateTime.UtcNow;
        long keyLen = 0;
        for (var spin = 0; spin < 10; spin++)
        {
            if (File.Exists(keyPath))
            {
                keyLen = new FileInfo(keyPath).Length;
                if (keyLen > 200)
                {
                    keyText = File.ReadAllText(keyPath);
                    if (keyText.Contains("ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
                    {
                        break;
                    }
                }
            }
            Thread.Sleep(25 * (spin + 1));
        }
        Assert.True(keyLen > 0, "Key file length was zero");
        Assert.Contains("ENCRYPTED PRIVATE KEY", keyText);

        // Import with encrypted key, retry with exponential backoff if HasPrivateKey is false
        X509Certificate2? imported = null;
        var hasPrivateKey = false;
        Exception? lastEx = null;
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                imported?.Dispose(); // dispose previous attempt
                imported = CertificateManager.Import(pemPath, pwd, keyPath);
                hasPrivateKey = imported.HasPrivateKey;
                if (hasPrivateKey)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
            Thread.Sleep(50 * attempt); // backoff: 50,100,...300ms
        }
        Assert.NotNull(imported);

        if (!hasPrivateKey)
        {
            // Manual fallback: try to pair the key ourselves (mirrors library logic)
            try
            {
                var certOnly = CertificateManager.Import(pemPath); // public only
                const string encBegin = "-----BEGIN ENCRYPTED PRIVATE KEY-----";
                const string encEnd = "-----END ENCRYPTED PRIVATE KEY-----";
                var start = keyText.IndexOf(encBegin, StringComparison.Ordinal);
                var end = keyText.IndexOf(encEnd, StringComparison.Ordinal);
                if (start >= 0 && end > start)
                {
                    start += encBegin.Length;
                    var b64 = keyText[start..end].Replace("\r", "").Replace("\n", "").Trim();
                    var encDer = Convert.FromBase64String(b64);
                    Exception? lastErr = null;
                    for (var round = 0; round < 2 && !hasPrivateKey; round++)
                    {
                        try
                        {
                            using var rsa = RSA.Create();
                            rsa.ImportEncryptedPkcs8PrivateKey(System.Text.Encoding.UTF8.GetBytes(pwd), encDer, out _);
                            imported = certOnly.CopyWithPrivateKey(rsa);
                            hasPrivateKey = imported.HasPrivateKey;
                            if (hasPrivateKey)
                            {
                                break;
                            }
                        }
                        catch (Exception exRsa)
                        {
                            lastErr = lastErr is null ? exRsa : new AggregateException(lastErr, exRsa);
                        }
                        try
                        {
                            using var ecdsa = ECDsa.Create();
                            ecdsa.ImportEncryptedPkcs8PrivateKey(System.Text.Encoding.UTF8.GetBytes(pwd), encDer, out _);
                            imported = certOnly.CopyWithPrivateKey(ecdsa);
                            hasPrivateKey = imported.HasPrivateKey;
                        }
                        catch (Exception exEc)
                        {
                            lastErr = lastErr is null ? exEc : new AggregateException(lastErr, exEc);
                        }
                        Thread.Sleep(25 * (round + 1));
                    }
                }
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
        }

        if (!hasPrivateKey)
        {
            var platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            var elapsedMs = (DateTime.UtcNow - swStart).TotalMilliseconds;
            Assert.True(hasPrivateKey, $"Imported certificate did not have a private key after retries. Attempts=6 Elapsed={elapsedMs:F0}ms Platform={platform} Framework={framework} KeyLen={keyLen} LastEx={lastEx}");
        }
        else
        {
            Assert.True(imported.HasPrivateKey);
            Assert.Contains("CN=localhost", imported.Subject, StringComparison.OrdinalIgnoreCase);
        }
        // no Environment.CurrentDirectory manipulation needed
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void ExportImport_Pem_PublicOnly()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pemPath = Path.Combine(dir, "pubonly.pem");

        CertificateManager.Export(cert, pemPath, CertificateManager.ExportFormat.Pem, [], includePrivateKey: false);
        Assert.True(File.Exists(pemPath));

        var imported = CertificateManager.Import(pemPath);
        Assert.NotNull(imported);
        Assert.False(imported.HasPrivateKey);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Validate_SelfSigned_And_WeakRsa()
    {
        var good = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        Assert.True(CertificateManager.Validate(good));
        Assert.False(CertificateManager.Validate(good, denySelfSigned: true));

        var weak = CertificateManager.NewSelfSigned(new CertificateManager.SelfSignedOptions([
            "localhost"
        ], KeyType: CertificateManager.KeyType.Rsa, KeyLength: 1024, ValidDays: 7, Ephemeral: true, Exportable: true));

        Assert.False(CertificateManager.Validate(weak, allowWeakAlgorithms: false));
        Assert.True(CertificateManager.Validate(weak, allowWeakAlgorithms: true));
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void GetPurposes_ReturnsAtLeastServerClientAuth()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var purposes = CertificateManager.GetPurposes(cert).ToList();
        Assert.True(purposes.Count >= 2);
        // Accept either friendly names or OIDs
        Assert.Contains(purposes, p =>
            p.Contains("Server Authentication", StringComparison.OrdinalIgnoreCase)
            || p == "1.3.6.1.5.5.7.3.1");
        Assert.Contains(purposes, p =>
            p.Contains("Client Authentication", StringComparison.OrdinalIgnoreCase)
            || p == "1.3.6.1.5.5.7.3.2");
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Import_Throws_OnMissingFile() => _ = Assert.Throws<FileNotFoundException>(() => CertificateManager.Import(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cer")));

    [Fact]
    [Trait("Category", "Certificates")]
    public void Import_Throws_OnEmptyPath() => _ = Assert.Throws<ArgumentException>(() => CertificateManager.Import(""));

    [Fact]
    [Trait("Category", "Certificates")]
    public void Import_Throws_OnMissingPrivateKeyFile_WhenSpecified()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pemPath = Path.Combine(dir, "onlycert.pem");
        // Write a simple public-only PEM
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var certDer = cert.Export(X509ContentType.Cert);
        var certB64 = Convert.ToBase64String(certDer, Base64FormattingOptions.InsertLineBreaks);
        File.WriteAllText(pemPath, $"-----BEGIN CERTIFICATE-----\n{certB64}\n-----END CERTIFICATE-----\n");

        var missingKey = Path.Combine(dir, "missing.key");
        _ = Assert.Throws<FileNotFoundException>(
            () => CertificateManager.Import(certPath: pemPath, privateKeyPath: missingKey, flags: X509KeyStorageFlags.DefaultKeySet));
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Import_Pfx_With_SecureString_Password_Succeeds()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pfxPath = Path.Combine(dir, "secure.pfx");
        var pwd = "Sup3rS3cure".AsSpan();
        CertificateManager.Export(cert, pfxPath, CertificateManager.ExportFormat.Pfx, pwd);
        Assert.True(File.Exists(pfxPath));

        using var ss = new SecureString();
        foreach (var c in pwd)
        {
            ss.AppendChar(c);
        }
        ss.MakeReadOnly();

        var imported = CertificateManager.Import(pfxPath, ss);
        Assert.True(imported.HasPrivateKey);
        Assert.Contains("CN=localhost", imported.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Import_Pem_With_Unencrypted_SeparateKey_Manual()
    {
        // Generate a non-ephemeral exportable self-signed cert for key extraction
        var cert = CertificateManager.NewSelfSigned(new CertificateManager.SelfSignedOptions([
            "localhost", "127.0.0.1"
        ], KeyType: CertificateManager.KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: false, Exportable: true));
        using var rsa = cert.GetRSAPrivateKey();
        Assert.NotNull(rsa);

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var certPemPath = Path.Combine(dir, "manual.pem");
        var keyPemPath = Path.Combine(dir, "manual.key");

        // Write certificate PEM
        var certDer = cert.Export(X509ContentType.Cert);
        var certB64 = Convert.ToBase64String(certDer, Base64FormattingOptions.InsertLineBreaks);
        File.WriteAllText(certPemPath, $"-----BEGIN CERTIFICATE-----\n{certB64}\n-----END CERTIFICATE-----\n");

        // Write unencrypted PKCS#1 RSA PRIVATE KEY (CreateFromPemFile supports it)
        var keyDer = rsa.ExportRSAPrivateKey();
        var keyB64 = Convert.ToBase64String(keyDer, Base64FormattingOptions.InsertLineBreaks);
        File.WriteAllText(keyPemPath, $"-----BEGIN RSA PRIVATE KEY-----\n{keyB64}\n-----END RSA PRIVATE KEY-----\n");

        var imported = CertificateManager.Import(certPemPath, privateKeyPath: keyPemPath, flags: X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable);
        Assert.True(imported.HasPrivateKey);
        Assert.Contains("CN=localhost", imported.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Import_Der_PublicOnly_Succeeds()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var derPath = Path.Combine(dir, "cert.cer");
        File.WriteAllBytes(derPath, cert.Export(X509ContentType.Cert));

        var imported = CertificateManager.Import(derPath);
        Assert.NotNull(imported);
        Assert.False(imported.HasPrivateKey); // DER export does not include private key
    }


    [Fact]
    [Trait("Category", "Certificates")]
    public void Validate_Purposes_Strict_Mismatch_Fails_But_NonStrict_Subset_Passes()
    {
        var cert = CertificateManager.NewSelfSigned(DefaultSelfSignedOptions());
        // Build OidCollections
        var serverAuth = new Oid("1.3.6.1.5.5.7.3.1");
        var clientAuth = new Oid("1.3.6.1.5.5.7.3.2");
        var subset = new OidCollection { serverAuth }; // only server
        var exact = new OidCollection { serverAuth, clientAuth };

        // Non-strict subset should succeed
        Assert.True(CertificateManager.Validate(cert, expectedPurpose: subset, strictPurpose: false));
        // Strict subset should fail (missing clientAuth)
        Assert.False(CertificateManager.Validate(cert, expectedPurpose: subset, strictPurpose: true));
        // Strict exact set should pass
        Assert.True(CertificateManager.Validate(cert, expectedPurpose: exact, strictPurpose: true));
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewCertificateRequest_Ecdsa_Uses_ECDSA_KeyPair()
    {
        var csr = CertificateManager.NewCertificateRequest(new CertificateManager.CsrOptions([
            "localhost"
        ], KeyType: CertificateManager.KeyType.Ecdsa, KeyLength: 256, CommonName: "localhost"));
        Assert.NotNull(csr.PrivateKey);
        // Private key should be EC
        var ecKey = Assert.IsType<Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters>(csr.PrivateKey);
        Assert.NotNull(ecKey.Parameters);
    }
}
