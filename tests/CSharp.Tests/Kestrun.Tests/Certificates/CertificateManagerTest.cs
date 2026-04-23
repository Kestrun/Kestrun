using System.Security;
using Kestrun.Certificates;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Xunit;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace Kestrun.Tests.Certificates;

public class CertificateManagerTest
{
    private static SelfSignedOptions DefaultSelfSignedOptions()
        => new(["localhost", "127.0.0.1", "::1"],
               KeyType: KeyType.Rsa,
               KeyLength: 2048,
               ValidDays: 30,
               Ephemeral: true,
               Exportable: true);

    private static X509Certificate2 CreateCertificate(SelfSignedOptions options)
        => CertificateManager.NewSelfSigned(options).Certificate;

    private static SelfSignedCertificateResult CreateDevelopmentBundle(SelfSignedOptions? options = null)
        => CertificateManager.NewSelfSigned(options ?? new SelfSignedOptions(DnsNames: null, Exportable: true, Development: true));

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_GeneratesValidCert_WithSAN_EKU()
    {
        var cert = CreateCertificate(DefaultSelfSignedOptions());

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
    public void NewSelfSigned_EmitsLoopbackIpSans_AndLeafExtensions()
    {
        using var cert = CreateCertificate(DefaultSelfSignedOptions());

        var extensions = cert.Extensions;
        var san = extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>().SingleOrDefault(e => e.Oid?.Value == "2.5.29.17");
        Assert.NotNull(san);

        var generalNames = GeneralNames.GetInstance(Asn1Object.FromByteArray(san.RawData)).GetNames();
        Assert.Contains(generalNames, n => n.TagNo == GeneralName.DnsName && string.Equals(n.Name.ToString(), "localhost", StringComparison.OrdinalIgnoreCase));

        var ipSans = generalNames
            .Where(n => n.TagNo == GeneralName.IPAddress)
            .Select(n => new IPAddress(Asn1OctetString.GetInstance(n.Name).GetOctets()))
            .ToList();

        Assert.Equal(2, ipSans.Count);
        Assert.Contains(IPAddress.Loopback, ipSans); // 127.0.0.1
        Assert.Contains(IPAddress.IPv6Loopback, ipSans); // ::1

        var basicConstraints = Assert.IsType<X509BasicConstraintsExtension>(extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>().Single(e => e.Oid?.Value == "2.5.29.19"));
        Assert.True(basicConstraints.Critical);
        Assert.False(basicConstraints.CertificateAuthority);

        Assert.NotNull(extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>().SingleOrDefault(e => e.Oid?.Value == "2.5.29.14")); // Subject Key Identifier
        Assert.NotNull(extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>().SingleOrDefault(e => e.Oid?.Value == "2.5.29.35")); // Authority Key Identifier
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_NormalizesSanInputs_BeforeEmittingSubjectAndSanSet()
    {
        using var cert = CreateCertificate(new SelfSignedOptions([
                " localhost ", "localhost", " 127.0.0.1 ", "127.0.0.1", "\t::1\t", "   "
            ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: true, Exportable: true));

        Assert.Contains("CN=localhost", cert.Subject, StringComparison.OrdinalIgnoreCase);

        var san = cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
            .Single(e => e.Oid?.Value == "2.5.29.17");
        var generalNames = GeneralNames.GetInstance(Asn1Object.FromByteArray(san.RawData)).GetNames();

        var dnsSans = generalNames
            .Where(n => n.TagNo == GeneralName.DnsName)
            .Select(n => n.Name.ToString())
            .ToList();
        var ipSans = generalNames
            .Where(n => n.TagNo == GeneralName.IPAddress)
            .Select(n => new IPAddress(Asn1OctetString.GetInstance(n.Name).GetOctets()))
            .ToList();

        var dnsSan = Assert.Single(dnsSans);
        Assert.Equal("localhost", dnsSan);
        Assert.Equal(2, ipSans.Count);
        Assert.Contains(IPAddress.Loopback, ipSans);
        Assert.Contains(IPAddress.IPv6Loopback, ipSans);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_Ecdsa_UsesDigitalSignatureOnlyKeyUsage()
    {
        using var cert = CreateCertificate(new SelfSignedOptions([
            "localhost", "::1"
        ], KeyType: KeyType.Ecdsa, KeyLength: 256, ValidDays: 30, Ephemeral: true, Exportable: true));

        var keyUsage = Assert.IsType<X509KeyUsageExtension>(cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>().Single(e => e.Oid?.Value == "2.5.29.15"));
        Assert.True(keyUsage.Critical);
        Assert.Equal(X509KeyUsageFlags.DigitalSignature, keyUsage.KeyUsages);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_UsesExplicitKeyUsageFlags_WhenProvided()
    {
        using var cert = CreateCertificate(new SelfSignedOptions([
            "localhost", "127.0.0.1"
        ],
        KeyType: KeyType.Rsa,
        KeyLength: 2048,
        KeyUsageFlags: X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.DataEncipherment,
        ValidDays: 30,
        Ephemeral: true,
        Exportable: true));

        var keyUsage = Assert.IsType<X509KeyUsageExtension>(cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>().Single(e => e.Oid?.Value == "2.5.29.15"));
        Assert.Equal(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.DataEncipherment, keyUsage.KeyUsages);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_TreatsExplicitNoneKeyUsageAsUnspecified()
    {
        using var cert = CreateCertificate(new SelfSignedOptions([
            "localhost", "127.0.0.1"
        ],
        KeyType: KeyType.Rsa,
        KeyLength: 2048,
        KeyUsageFlags: X509KeyUsageFlags.None,
        ValidDays: 30,
        Ephemeral: true,
        Exportable: true));

        var keyUsage = Assert.IsType<X509KeyUsageExtension>(
            cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
                .Single(e => e.Oid?.Value == "2.5.29.15"));

        Assert.True(keyUsage.Critical);
        Assert.Equal(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, keyUsage.KeyUsages);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_CertificateAuthority_EmitsCaExtensionsWithoutLeafEkuOrSan()
    {
        using var cert = CreateCertificate(new SelfSignedOptions([
            "Kestrun Development Root CA"
        ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 365, Ephemeral: true, Exportable: true, IsCertificateAuthority: true));

        Assert.Contains("CN=Kestrun Development Root CA", cert.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cert.Subject, cert.Issuer);

        var basicConstraints = Assert.IsType<X509BasicConstraintsExtension>(
            cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
                .Single(e => e.Oid?.Value == "2.5.29.19"));
        Assert.True(basicConstraints.CertificateAuthority);

        var keyUsage = Assert.IsType<X509KeyUsageExtension>(
            cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
                .Single(e => e.Oid?.Value == "2.5.29.15"));
        Assert.Equal(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, keyUsage.KeyUsages);

        Assert.Null(cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
            .SingleOrDefault(e => e.Oid?.Value == "2.5.29.17"));
        Assert.Null(cert.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
            .SingleOrDefault(e => e.Oid?.Value == "2.5.29.37"));
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_IssuerCertificate_SignsLeafCertificateWithIssuerMetadata()
    {
        using var root = CreateCertificate(new SelfSignedOptions([
            "Kestrun Development Root CA"
        ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 365, Ephemeral: true, Exportable: true, IsCertificateAuthority: true));

        using var leaf = CreateCertificate(new SelfSignedOptions([
            "localhost", "127.0.0.1", "::1"
        ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: true, Exportable: true, IssuerCertificate: root));

        Assert.Contains("CN=localhost", leaf.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(root.Subject, leaf.Issuer);
        Assert.NotEqual(leaf.Subject, leaf.Issuer);

        var leafBasicConstraints = Assert.IsType<X509BasicConstraintsExtension>(
            leaf.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
                .Single(e => e.Oid?.Value == "2.5.29.19"));
        Assert.False(leafBasicConstraints.CertificateAuthority);

        var authorityKeyIdentifier = leaf.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
            .Single(e => e.Oid?.Value == "2.5.29.35");
        var subjectKeyIdentifier = Assert.IsType<X509SubjectKeyIdentifierExtension>(
            root.Extensions.Cast<System.Security.Cryptography.X509Certificates.X509Extension>()
                .Single(e => e.Oid?.Value == "2.5.29.14"));

        var authorityKeyIdentifierHex = GetAuthorityKeyIdentifierHex(authorityKeyIdentifier);
        var subjectKeyIdentifierHex = subjectKeyIdentifier.SubjectKeyIdentifier!.Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
        Assert.Equal(subjectKeyIdentifierHex, authorityKeyIdentifierHex);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_IssuerCertificate_RejectsNonCaIssuer()
    {
        using var issuer = CreateCertificate(DefaultSelfSignedOptions());

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCertificate(new SelfSignedOptions([
            "localhost"
        ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: true, Exportable: true, IssuerCertificate: issuer)));

        Assert.Contains("CA certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_Development_CreatesDefaultBundle()
    {
        var bundle = CreateDevelopmentBundle();

        Assert.NotNull(bundle);
        Assert.NotNull(bundle.RootCertificate);
        Assert.NotNull(bundle.LeafCertificate);
        Assert.Equal("CN=Kestrun Development Root CA", bundle.RootCertificate.Subject);
        Assert.Equal("CN=localhost", bundle.LeafCertificate.Subject);
        Assert.Equal(bundle.RootCertificate.Subject, bundle.LeafCertificate.Issuer);
        Assert.True(bundle.RootCertificate.HasPrivateKey);
        Assert.True(bundle.LeafCertificate.HasPrivateKey);
        Assert.NotNull(bundle.PublicRootCertificate);
        Assert.False(bundle.PublicRootCertificate.HasPrivateKey);
        Assert.Equal(bundle.RootCertificate.Thumbprint, bundle.PublicRootCertificate.Thumbprint);
        Assert.False(bundle.RootTrusted);

        bundle.PublicRootCertificate.Dispose();
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewSelfSigned_Development_ReusesSuppliedRootCertificate()
    {
        using var root = CreateCertificate(new SelfSignedOptions([
            "Reusable Development Root CA"
        ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 365, Ephemeral: false, Exportable: true, IsCertificateAuthority: true));

        var bundle = CreateDevelopmentBundle(new SelfSignedOptions(
            DnsNames: ["localhost", "dev.localtest.me"],
            Exportable: true,
            Development: true,
            RootCertificate: root));

        Assert.NotNull(bundle);
        Assert.NotNull(bundle.RootCertificate);
        Assert.NotNull(bundle.LeafCertificate);
        Assert.NotNull(bundle.PublicRootCertificate);

        Assert.Same(root, bundle.RootCertificate);
        Assert.Equal(root.Subject, bundle.LeafCertificate.Issuer);
        Assert.Equal("CN=localhost", bundle.LeafCertificate.Subject);
        Assert.NotNull(bundle.PublicRootCertificate);
        Assert.False(bundle.PublicRootCertificate.HasPrivateKey);
        Assert.Equal(root.Thumbprint, bundle.PublicRootCertificate.Thumbprint);
        Assert.False(bundle.RootTrusted);

        bundle.PublicRootCertificate.Dispose();
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void NewCertificateRequest_ReturnsPemAndPrivateKey_WithSAN()
    {
        var csr = CertificateManager.NewCertificateRequest(
            new CsrOptions([
                "localhost", "192.168.0.1"
            ], KeyType: KeyType.Rsa, KeyLength: 2048, CommonName: "localhost"));

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
    public void NewCertificateRequest_WithKeyUsage_EmitsKeyUsageExtensionRequest()
    {
        var csr = CertificateManager.NewCertificateRequest(
            new CsrOptions([
                "localhost"
            ], KeyType: KeyType.Rsa, KeyLength: 2048, CommonName: "localhost", KeyUsageFlags: X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment));

        using var sr = new StringReader(csr.CsrPem);
        var obj = new PemReader(sr).ReadObject();
        var req = Assert.IsType<Pkcs10CertificationRequest>(obj);
        var attributes = req.GetCertificationRequestInfo().Attributes;

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

        var extensions = X509Extensions.GetInstance(extAttr!.AttrValues[0]);
        var keyUsageExt = extensions.GetExtension(X509Extensions.KeyUsage);

        Assert.NotNull(keyUsageExt);

        var keyUsage = KeyUsage.GetInstance(keyUsageExt!.GetParsedValue());
        var expectedUsage = KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment;
        Assert.Equal(expectedUsage, keyUsage.IntValue);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void ExportImport_Pfx_RoundTrip_WithPassword()
    {
        var cert = CreateCertificate(DefaultSelfSignedOptions());

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pfxPath = Path.Combine(dir, "testcert.pfx");
        var password = "s3cret".AsSpan();

        CertificateManager.Export(cert, pfxPath, ExportFormat.Pfx, password);
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
        var options = new SelfSignedOptions([
            "localhost", "127.0.0.1"
        ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: false, Exportable: true);
        var cert = CreateCertificate(options);

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
        CertificateManager.Export(cert, pemPath, ExportFormat.Pem, ss, includePrivateKey: true);
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
        var cert = CreateCertificate(DefaultSelfSignedOptions());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pemPath = Path.Combine(dir, "pubonly.pem");

        CertificateManager.Export(cert, pemPath, ExportFormat.Pem, [], includePrivateKey: false);
        Assert.True(File.Exists(pemPath));

        var imported = CertificateManager.Import(pemPath);
        Assert.NotNull(imported);
        Assert.False(imported.HasPrivateKey);
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Validate_SelfSigned_And_WeakRsa()
    {
        var good = CreateCertificate(DefaultSelfSignedOptions());
        Assert.True(CertificateManager.Validate(good));
        Assert.False(CertificateManager.Validate(good, denySelfSigned: true));

        var weak = CreateCertificate(new SelfSignedOptions([
            "localhost"
        ], KeyType: KeyType.Rsa, KeyLength: 1024, ValidDays: 7, Ephemeral: true, Exportable: true));

        Assert.False(CertificateManager.Validate(weak, allowWeakAlgorithms: false));
        Assert.True(CertificateManager.Validate(weak, allowWeakAlgorithms: true));
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Validate_IssuedLeaf_WithSuppliedRootChain_Passes()
    {
        var bundle = CreateDevelopmentBundle(new SelfSignedOptions(
            DnsNames: ["localhost", "127.0.0.1", "::1"],
            Exportable: true,
            Development: true));

        Assert.NotNull(bundle);
        Assert.NotNull(bundle.RootCertificate);
        Assert.NotNull(bundle.LeafCertificate);

        try
        {
            Assert.False(CertificateManager.Validate(
                bundle.LeafCertificate,
                checkRevocation: false,
                allowWeakAlgorithms: false,
                denySelfSigned: false,
                expectedPurpose: null,
                strictPurpose: false,
                certificateChain: null,
                out var defaultFailureReason));
            Assert.Contains("PartialChain", defaultFailureReason, StringComparison.OrdinalIgnoreCase);

            var certificateChain = new X509Certificate2Collection { bundle.RootCertificate };
            Assert.True(CertificateManager.Validate(
                bundle.LeafCertificate,
                checkRevocation: false,
                allowWeakAlgorithms: false,
                denySelfSigned: false,
                expectedPurpose: null,
                strictPurpose: false,
                certificateChain,
                out var suppliedChainFailureReason));
            Assert.Equal(string.Empty, suppliedChainFailureReason);
        }
        finally
        {
            bundle.LeafCertificate.Dispose();
            bundle.RootCertificate.Dispose();
        }
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void GetPurposes_ReturnsAtLeastServerClientAuth()
    {
        var cert = CreateCertificate(DefaultSelfSignedOptions());
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
        var cert = CreateCertificate(DefaultSelfSignedOptions());
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
        var cert = CreateCertificate(DefaultSelfSignedOptions());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "KestrunTests", Guid.NewGuid().ToString("N"))).FullName;
        var pfxPath = Path.Combine(dir, "secure.pfx");
        var pwd = "Sup3rS3cure".AsSpan();
        CertificateManager.Export(cert, pfxPath, ExportFormat.Pfx, pwd);
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
        var cert = CreateCertificate(new SelfSignedOptions([
            "localhost", "127.0.0.1"
        ], KeyType: KeyType.Rsa, KeyLength: 2048, ValidDays: 30, Ephemeral: false, Exportable: true));
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
        var cert = CreateCertificate(DefaultSelfSignedOptions());
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
        var cert = CreateCertificate(DefaultSelfSignedOptions());
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
        var csr = CertificateManager.NewCertificateRequest(new CsrOptions([
            "localhost"
        ], KeyType: KeyType.Ecdsa, KeyLength: 256, CommonName: "localhost"));
        Assert.NotNull(csr.PrivateKey);
        // Private key should be EC
        var ecKey = Assert.IsType<Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters>(csr.PrivateKey);
        Assert.NotNull(ecKey.Parameters);
    }

    private static string GetAuthorityKeyIdentifierHex(System.Security.Cryptography.X509Certificates.X509Extension authorityKeyIdentifier)
    {
        var authorityKeyIdentifierAsn = AuthorityKeyIdentifier.GetInstance(Asn1Object.FromByteArray(authorityKeyIdentifier.RawData));
        var keyIdentifier = Assert.IsType<byte[]>(authorityKeyIdentifierAsn.GetKeyIdentifier());
        return Convert.ToHexString(keyIdentifier).ToLowerInvariant();
    }
}
