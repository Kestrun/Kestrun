using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Security;
using System.Runtime.InteropServices;
using Kestrun.Models;  // For Base64UrlEncoder

namespace Kestrun.Jwt;

/// <summary>
/// Fluent utility to create any flavour of JWS/JWE in one line.
/// </summary>
/// <example>
/// // PowerShell usage:
/// $builder = [Kestrun.Security.JwtTokenBuilder]::New()
/// $token   = $builder
///             .WithSubject('admin')
///             .WithIssuer('https://issuer')
///             .WithAudience('api')
///             .SignWithSecret('uZ6zDP3CGK3rktmVOXQk8A')   # base64url
///             .EncryptWithCertificate($cert,'RSA-OAEP','A256GCM')
///             .Build()
/// Write-Output $token
/// </example>
public sealed class JwtTokenBuilder
{
    // ───── Public fluent API ──────────────────────────────────────────
    /// <summary>
    /// Creates a new instance of <see cref="JwtTokenBuilder"/>.
    /// </summary>
    /// <returns>A new <see cref="JwtTokenBuilder"/> instance.</returns>
    public static JwtTokenBuilder New() => new();

    /// <summary>
    /// Sets the issuer of the JWT token.
    /// </summary>
    /// <param name="issuer">The issuer to set.</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder WithIssuer(string issuer) { _issuer = issuer; return this; }
    /// <summary>
    /// Sets the audience of the JWT token.
    /// </summary>
    /// <param name="audience">The audience to set.</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder WithAudience(string audience) { _aud = audience; return this; }
    /// <summary>
    /// Sets the subject ('sub' claim) of the JWT token.
    /// </summary>
    /// <param name="sub">The subject to set.</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder WithSubject(string sub) { _claims.Add(new Claim(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub, sub)); return this; }
    /// <summary>
    /// Adds a claim to the JWT token.
    /// </summary>
    /// <param name="type">The claim type.</param>
    /// <param name="value">The claim value.</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder AddClaim(string type, string value) { _claims.Add(new Claim(type, value)); return this; }
    /// <summary>
    /// Sets the lifetime (validity period) of the JWT token.
    /// </summary>
    /// <param name="ttl">The time span for which the token is valid.</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder ValidFor(TimeSpan ttl) { _lifetime = ttl; return this; }
    /// <summary>
    /// Sets the 'not before' (nbf) claim for the JWT token.
    /// </summary>
    /// <param name="utc">The UTC date and time before which the token is not valid.</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder NotBefore(DateTime utc) { _nbf = utc; return this; }
    /// <summary>
    /// Adds a custom header to the JWT token.
    /// </summary>
    /// <param name="k">The header key.</param>
    /// <param name="v">The header value.</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder AddHeader(string k, object v) { _header[k] = v; return this; }

    /// <summary>
    /// Gets the issuer of the JWT token.
    /// </summary>
    public string Issuer => _issuer ?? string.Empty;
    /// <summary>
    /// Gets the audience of the JWT token.
    /// </summary>
    public string Audience => _aud ?? string.Empty;
    /// <summary>
    /// Gets the algorithm used for signing the JWT token.
    /// </summary>
    public string? Algorithm { get; private set; }

    // ── pending-config “envelopes” (built later) ─────────
    private sealed record PendingSymmetricSign(string B64u, string Alg /*auto/HS256…*/);
    private sealed record PendingRsaSign(string Pem, string Alg);
    private sealed record PendingCertSign(X509Certificate2 Cert, string Alg);

    private sealed record PendingSymmetricEnc(string B64u, string KeyAlg, string EncAlg);
    private sealed record PendingRsaEnc(string Pem, string KeyAlg, string EncAlg);
    private sealed record PendingCertEnc(X509Certificate2 Cert, string KeyAlg, string EncAlg);

    private object? _pendingSign;     // will be one of the above
    private object? _pendingEnc;
    private SymmetricSecurityKey? _issuerSigningKey;

    // ── signing helpers (store only) ─────────────────────
    /// <summary>
    /// Signs the JWT using a symmetric key provided as a Base64Url-encoded string.
    /// </summary>
    /// <param name="b64Url">The symmetric key as a Base64Url-encoded string.</param>
    /// <param name="alg">The JWT algorithm to use for signing (default is Auto).</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder SignWithSecret(
       string b64Url,
       JwtAlgorithm alg = JwtAlgorithm.Auto)
    {
        if (string.IsNullOrWhiteSpace(b64Url))
        {
            throw new ArgumentNullException(nameof(b64Url));
        }

        // 1) Decode the incoming Base64Url to bytes
        var raw = Base64UrlEncoder.DecodeBytes(b64Url);

        // 2) Create (and remember) the SymmetricSecurityKey
        var key = new SymmetricSecurityKey(raw)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        _issuerSigningKey = key;

        // 3) Resolve "Auto" or map the enum to the exact JWS alg string
        var resolvedAlg = alg.ToJwtString(raw.Length);

        // 4) Store the pending sign using the resolved algorithm
        _pendingSign = new PendingSymmetricSign(b64Url, resolvedAlg);

        return this;
    }


    /// <summary>
    /// Creates a new token builder instance by cloning the current configuration.
    /// </summary>
    /// <returns>A new <see cref="JwtTokenBuilder"/> instance with the same configuration.</returns>
    public JwtTokenBuilder CloneBuilder()
    {
        var clone = (JwtTokenBuilder)MemberwiseClone();
        clone._claims = [.. _claims];
        return clone;
    }

    /// <summary>
    /// Signs the JWT using a symmetric key provided as a hexadecimal string.
    /// </summary>
    /// <param name="hex">The symmetric key as a hexadecimal string.</param>
    /// <param name="alg">The JWT algorithm to use for signing (default is Auto).</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder SignWithSecretHex(string hex, JwtAlgorithm alg = JwtAlgorithm.Auto) => SignWithSecret(Base64UrlEncoder.Encode(Convert.FromHexString(hex)), alg);

    /// <summary>
    /// Signs the JWT using a symmetric key derived from the provided passphrase.
    /// </summary>
    /// <param name="passPhrase">The passphrase to use as the symmetric key.</param>
    /// <param name="alg">The JWT algorithm to use for signing (default is Auto).</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder SignWithSecretPassphrase(
       SecureString passPhrase,
       JwtAlgorithm alg = JwtAlgorithm.Auto)
    {
        ArgumentNullException.ThrowIfNull(passPhrase);

        // Marshal to unmanaged Unicode (UTF-16) buffer
        var unicodePtr = Marshal.SecureStringToGlobalAllocUnicode(passPhrase);
        try
        {
            var charCount = passPhrase.Length;
            var unicodeBytes = new byte[charCount * sizeof(char)];
            // copy from unmanaged -> managed
            Marshal.Copy(unicodePtr, unicodeBytes, 0, unicodeBytes.Length);

            // convert UTF-16 bytes directly to UTF-8
            var utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, unicodeBytes);
            // zero-out the intermediate Unicode bytes
            Array.Clear(unicodeBytes, 0, unicodeBytes.Length);

            var b64url = Base64UrlEncoder.Encode(utf8Bytes);
            // zero-out the UTF-8 bytes too
            Array.Clear(utf8Bytes, 0, utf8Bytes.Length);

            return SignWithSecret(b64url, alg);
        }
        finally
        {
            // zero-free the unmanaged buffer
            Marshal.ZeroFreeGlobalAllocUnicode(unicodePtr);
        }
    }

    // ── inside JwtTokenBuilder ─────────────────────────────────────────

    /// <summary>
    /// Signs the JWT using an RSA private key provided in PEM format.
    /// </summary>
    /// <param name="pemPath">The file path to the RSA private key in PEM format.</param>
    /// <param name="alg">The JWT algorithm to use for signing (default is Auto).</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder SignWithRsaPem(
        string pemPath,
        JwtAlgorithm alg = JwtAlgorithm.Auto)
    {
        var pem = File.ReadAllText(pemPath);

        // Auto ⇒ default RS256; otherwise map enum to the exact string
        var resolvedAlg = alg == JwtAlgorithm.Auto
            ? SecurityAlgorithms.RsaSha256
            : alg.ToJwtString(0);

        _pendingSign = new PendingRsaSign(pem, resolvedAlg);
        return this;
    }

    /// <summary>
    /// Sign with an X.509 certificate (must have private key).
    /// </summary>
    public JwtTokenBuilder SignWithCertificate(
        X509Certificate2 cert,
        JwtAlgorithm alg = JwtAlgorithm.Auto)
    {
        if (!cert.HasPrivateKey)
        {
            throw new ArgumentException(
                "Certificate must contain a private key.", nameof(cert));
        }

        // Auto ⇒ ES256 for ECDSA keys, RS256 for RSA keys
        var resolvedAlg = alg == JwtAlgorithm.Auto
            ? (cert.GetECDsaPublicKey() is not null
                ? SecurityAlgorithms.EcdsaSha256
                : SecurityAlgorithms.RsaSha256)
            : alg.ToJwtString(0);

        _pendingSign = new PendingCertSign(cert, resolvedAlg);
        return this;
    }



    // ── encryption helpers (lazy) ───────────────────────────────────

    // 1️⃣  X.509 certificate (RSA or EC public key)
    /// <summary>
    /// Encrypts the JWT using the provided X.509 certificate.
    /// </summary>
    /// <param name="cert">The X.509 certificate to use for encryption.</param>
    /// <param name="keyAlg">The key encryption algorithm (default is "RSA-OAEP").</param>
    /// <param name="encAlg">The content encryption algorithm (default is "A256GCM").</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder EncryptWithCertificate(
        X509Certificate2 cert,
        string keyAlg = "RSA-OAEP",
        string encAlg = "A256GCM")
    {
        _pendingEnc = new PendingCertEnc(cert, keyAlg, encAlg);
        return this;
    }

    /// <summary>
    /// Encrypts the JWT using a PEM-encoded RSA public key.
    /// </summary>
    /// <param name="pemPath">The file path to the PEM-encoded RSA public key.</param>
    /// <param name="keyAlg">The key encryption algorithm (default is "RSA-OAEP").</param>
    /// <param name="encAlg">The content encryption algorithm (default is "A256GCM").</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder EncryptWithPemPublic(
        string pemPath,
        string keyAlg = "RSA-OAEP",
        string encAlg = "A256GCM")
    {
        _pendingEnc = new PendingRsaEnc(File.ReadAllText(pemPath), keyAlg, encAlg);
        return this;
    }

    /// <summary>
    /// Encrypts the JWT using a symmetric key provided as a hexadecimal string.
    /// </summary>
    /// <param name="hex">The symmetric key as a hexadecimal string.</param>
    /// <param name="keyAlg">The key encryption algorithm (default is "dir").</param>
    /// <param name="encAlg">The content encryption algorithm (default is "A256CBC-HS512").</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder EncryptWithSecretHex(
        string hex,
        string keyAlg = "dir",
        string encAlg = "A256CBC-HS512") => EncryptWithSecret(Convert.FromHexString(hex), keyAlg, encAlg);

    /// <summary>
    /// Encrypts the JWT using a symmetric key provided as a Base64Url-encoded string.
    /// </summary>
    /// <param name="b64Url">The symmetric key as a Base64Url-encoded string.</param>
    /// <param name="keyAlg">The key encryption algorithm (default is "dir").</param>
    /// <param name="encAlg">The content encryption algorithm (default is "A256CBC-HS512").</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder EncryptWithSecretB64(
        string b64Url,
        string keyAlg = "dir",
        string encAlg = "A256CBC-HS512") => EncryptWithSecret(Base64UrlEncoder.DecodeBytes(b64Url), keyAlg, encAlg);

    /// <summary>
    /// Encrypts the JWT using a symmetric key provided as a byte array.
    /// </summary>
    /// <param name="keyBytes">The symmetric key as a byte array.</param>
    /// <param name="keyAlg">The key encryption algorithm (default is "dir").</param>
    /// <param name="encAlg">The content encryption algorithm (default is "A256CBC-HS512").</param>
    /// <returns>The current <see cref="JwtTokenBuilder"/> instance.</returns>
    public JwtTokenBuilder EncryptWithSecret(
        byte[] keyBytes,
        string keyAlg = "dir",
        string encAlg = "A256CBC-HS512")
    {
        var b64u = Base64UrlEncoder.Encode(keyBytes);
        _pendingEnc = new PendingSymmetricEnc(b64u, keyAlg, encAlg);
        return this;
    }


    // ───── Build the compact JWT ──────────────────────────────────────

    /// <summary>
    /// Builds the JWT token.
    /// This method constructs the JWT token using the configured parameters and returns it as a compact string.
    /// </summary>
    /// <returns>The JWT token as a compact string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no signing credentials are configured.</exception>
    /// <remarks>
    /// This method constructs the JWT token using the configured parameters and returns it as a compact string.
    /// </remarks>
    private string BuildToken()
    {
        var handler = new JwtSecurityTokenHandler();
        // ── build creds lazily now ───────────────────────────
        var signCreds = BuildSigningCredentials(out _issuerSigningKey) ?? throw new InvalidOperationException("No signing credentials configured.");
        Algorithm = signCreds.Algorithm;
        var encCreds = BuildEncryptingCredentials();
        if (_nbf < DateTime.UtcNow)
        {
            _nbf = DateTime.UtcNow;
        }
        var token = handler.CreateJwtSecurityToken(
            issuer: _issuer,
            audience: _aud,
            subject: new ClaimsIdentity(_claims),
            notBefore: _nbf,
            expires: _nbf.Add(_lifetime),
            issuedAt: DateTime.UtcNow,
            signingCredentials: signCreds,
            encryptingCredentials: encCreds);


        foreach (var kv in _header)
        {
            token.Header[kv.Key] = kv.Value;
        }

        return handler.WriteToken(token);
    }

    /// <summary>
    /// Builds the JWT token.
    /// This method constructs the JWT token using the configured parameters and returns it as a compact string.
    /// </summary>
    /// <param name="signingKey">The signing key used to sign the JWT.</param>
    /// <returns>The JWT token as a compact string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no signing credentials are configured.</exception>
    /// <remarks>
    /// This method constructs the JWT token using the configured parameters and returns it as a compact string.
    /// </remarks>
    private string BuildToken(out SymmetricSecurityKey? signingKey)
    {
        var jwt = BuildToken();          // call the original Build()
        signingKey = _issuerSigningKey;  // may be null for unsigned / RSA / cert
        return jwt;
    }


    /// <summary>
    /// Builds the JWT token and returns a <see cref="JwtBuilderResult"/> containing the token, signing key, and validity period.
    /// </summary>
    /// <returns>A <see cref="JwtBuilderResult"/> containing the JWT token, signing key, and validity period.</returns>
    public JwtBuilderResult Build()
    {
        // ① produce the raw token + signing key
        var token = BuildToken(out var key);
        // ② Parse it immediately to pull out the valid-from / valid-to
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var issuedAt = jwtToken.ValidFrom.ToUniversalTime();
        var expires = jwtToken.ValidTo.ToUniversalTime();
        // ③ return the helper object
        return new JwtBuilderResult(token, key, this, issuedAt, expires);
    }

    // ────────── helpers that materialise creds ───────────

    /// <summary>
    /// Builds the signing credentials.
    /// This method constructs the signing credentials based on the pending signing configuration.
    /// If no signing configuration is set, it returns null.
    /// </summary>
    /// <param name="key">The symmetric security key to use for signing.</param>
    /// <returns>The signing credentials, or null if not configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no signing configuration is set.</exception>
    /// <remarks>
    /// This method constructs the signing credentials based on the pending signing configuration.
    /// If no signing configuration is set, it returns null.
    /// </remarks>
    private SigningCredentials? BuildSigningCredentials(out SymmetricSecurityKey? key)
    {
        key = null;

        return _pendingSign switch
        {
            PendingSymmetricSign ps => CreateHsCreds(ps, out key),
            PendingRsaSign pr => CreateRsaCreds(pr),
            PendingCertSign pc => CreateCertCreds(pc),
            _ => null
        };
    }

    /// <summary>
    /// Builds the signing credentials.
    /// This method constructs the signing credentials based on the pending signing configuration.
    /// If no signing configuration is set, it returns null.
    /// </summary>
    /// <param name="ps">The pending symmetric signing configuration.</param>
    /// <param name="key">The symmetric security key to use for signing.</param>
    /// <returns>The signing credentials, or null if not configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no signing configuration is set.</exception>
    /// <remarks>
    /// This method constructs the signing credentials based on the pending symmetric signing configuration.
    /// If no signing configuration is set, it returns null.
    /// </remarks>
    private static SigningCredentials CreateHsCreds(
     PendingSymmetricSign ps,
     out SymmetricSecurityKey key)
    {
        // 1) decode the Base64Url secret
        var raw = Base64UrlEncoder.DecodeBytes(ps.B64u);

        // 2) create the SymmetricSecurityKey (and record its KeyId)
        key = new SymmetricSecurityKey(raw)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };

        // 3) ps.Alg is *already* the exact SecurityAlgorithms.* constant
        return new SigningCredentials(key, ps.Alg);
    }

    /// <summary>
    /// Creates signing credentials for RSA using the provided PEM string.
    /// This method imports the RSA key from the PEM string and returns the signing credentials.
    /// </summary>
    /// <param name="pr">The pending RSA signing configuration.</param>
    /// <returns>The signing credentials for RSA.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the PEM string is invalid or cannot be imported.</exception>
    /// <remarks>
    /// This method imports the RSA key from the PEM string and returns the signing credentials.
    /// </remarks>
    private static SigningCredentials CreateRsaCreds(PendingRsaSign pr)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pr.Pem);
        var key = new RsaSecurityKey(rsa)
        {
            KeyId = Guid.NewGuid().ToString("N")
        };
        return new SigningCredentials(key, pr.Alg);
    }
    /// <summary>
    /// Creates signing credentials for a certificate.
    /// </summary>
    /// <param name="pc">The pending certificate signing configuration.</param>
    /// <returns>The signing credentials for the certificate.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the certificate does not have a private key.</exception>
    /// <remarks>
    /// This method creates signing credentials for a certificate using the provided certificate.
    /// </remarks>
    private static SigningCredentials CreateCertCreds(PendingCertSign pc)
    {
        var cert = pc.Cert;
        var key = new X509SecurityKey(cert);  // thumbprint becomes kid
        return new SigningCredentials(key, pc.Alg);
    }
    /// <summary>
    /// Builds the encrypting credentials.
    /// This method constructs the encrypting credentials based on the pending encryption configuration.
    /// If no encryption configuration is set, it returns null.
    /// </summary>
    /// <returns>The encrypting credentials, or null if not set.</returns>
    /// <remarks>
    /// This method constructs the encrypting credentials based on the pending encryption configuration.
    /// If no encryption configuration is set, it returns null.
    /// </remarks>
    private EncryptingCredentials? BuildEncryptingCredentials()
        => _pendingEnc switch
        {
            PendingSymmetricEnc se => new SymmetricEncrypt(
                                          se.B64u, se.KeyAlg, se.EncAlg).ToEncryptingCreds(),
            PendingRsaEnc re => new RsaEncrypt(
                                          re.Pem, re.KeyAlg, re.EncAlg).ToEncryptingCreds(),
            PendingCertEnc ce => new CertEncrypt(
                                          ce.Cert, ce.KeyAlg, ce.EncAlg).ToEncryptingCreds(),
            _ => null
        };



    // ───── Internals ──────────────────────────────────────────────────
    /// <summary>
    /// Gets the claims to be included in the JWT token.
    /// </summary>
    private List<Claim> _claims = [];
    /// <summary>
    /// Gets the headers to be included in the JWT token.
    /// </summary>
    private readonly Dictionary<string, object> _header = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets the not before (nbf) claim for the JWT token.
    /// </summary>
    private DateTime _nbf = DateTime.UtcNow;
    /// <summary>
    /// Gets the lifetime of the JWT token.
    /// </summary>
    private TimeSpan _lifetime = TimeSpan.FromHours(1);
    private string? _issuer, _aud;


    // ── Strategy interfaces & concrete configs ───────────────────────
    private interface ISignConfig { SigningCredentials ToSigningCreds(); }
    private interface IEncConfig { EncryptingCredentials ToEncryptingCreds(); }

    private static class Map
    {
        // maps short names (HS256, RSA-OAEP, …) to SecurityAlgorithms
        public static readonly IReadOnlyDictionary<string, string> Jws = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HS256"] = SecurityAlgorithms.HmacSha256,
            ["HS384"] = SecurityAlgorithms.HmacSha384,
            ["HS512"] = SecurityAlgorithms.HmacSha512,
            ["RS256"] = SecurityAlgorithms.RsaSha256,
            ["RS384"] = SecurityAlgorithms.RsaSha384,
            ["RS512"] = SecurityAlgorithms.RsaSha512,
            ["PS256"] = SecurityAlgorithms.RsaSsaPssSha256,
            ["PS384"] = SecurityAlgorithms.RsaSsaPssSha384,
            ["PS512"] = SecurityAlgorithms.RsaSsaPssSha512,
            ["ES256"] = SecurityAlgorithms.EcdsaSha256,
            ["ES384"] = SecurityAlgorithms.EcdsaSha384,
            ["ES512"] = SecurityAlgorithms.EcdsaSha512
        };
        public static readonly IReadOnlyDictionary<string, string> KeyAlg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RSA-OAEP"] = SecurityAlgorithms.RsaOAEP,
            ["RSA-OAEP-256"] = "RSA-OAEP-256",
            ["RSA-OAEP-384"] = "RSA-OAEP-384",
            ["RSA-OAEP-512"] = "RSA-OAEP-512",
            ["RSA1_5"] = SecurityAlgorithms.RsaPKCS1,
            ["A128KW"] = SecurityAlgorithms.Aes128KW,
            ["A192KW"] = SecurityAlgorithms.Aes192KW,
            ["A256KW"] = SecurityAlgorithms.Aes256KW,
            ["ECDH-ES"] = SecurityAlgorithms.EcdhEs,
            ["ECDH-ESA128KW"] = SecurityAlgorithms.EcdhEsA128kw,
            ["ECDH-ESA192KW"] = SecurityAlgorithms.EcdhEsA192kw,
            ["ECDH-ESA256KW"] = SecurityAlgorithms.EcdhEsA256kw,
            ["dir"] = "dir"
        };
        public static readonly IReadOnlyDictionary<string, string> EncAlg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A128GCM"] = SecurityAlgorithms.Aes128Gcm,
            ["A192GCM"] = SecurityAlgorithms.Aes192Gcm,
            ["A256GCM"] = SecurityAlgorithms.Aes256Gcm,
            ["A128CBC-HS256"] = SecurityAlgorithms.Aes128CbcHmacSha256,
            ["A192CBC-HS384"] = SecurityAlgorithms.Aes192CbcHmacSha384,
            ["A256CBC-HS512"] = SecurityAlgorithms.Aes256CbcHmacSha512
        };
    }

    // ── Signing configs ───────────────────────────────────────────────
    private sealed record SymmetricSign(
         SecurityKey Key, string ResolvedAlg) : ISignConfig
    {
        public SigningCredentials ToSigningCreds()
            => new(Key, ResolvedAlg);
    }

    private sealed record RsaSign(string Pem, string Alg) : ISignConfig
    {
        public SigningCredentials ToSigningCreds()
        {
            var rsa = RSA.Create(); rsa.ImportFromPem(Pem);
            var key = new RsaSecurityKey(rsa);
            var algo = Alg.Equals("auto", StringComparison.OrdinalIgnoreCase) ? Map.Jws["RS256"] : Map.Jws[Alg];
            return new SigningCredentials(key, algo);
        }
    }


    private sealed record CertSign(X509Certificate2 Cert, string Alg) : ISignConfig
    {
        public SigningCredentials ToSigningCreds()
        {
            if (!Cert.HasPrivateKey)
            {
                throw new ArgumentException("Certificate must contain a private key.");
            }

            var key = new X509SecurityKey(Cert);

            // Pick default alg if caller passed "auto"
            string resolvedAlg;
            if (!Alg.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                resolvedAlg = Map.Jws[Alg];
            }
            else
            {
                if (Cert.GetECDsaPublicKey() is not null)
                {
                    resolvedAlg = Map.Jws["ES256"];   // ECDSA → ES256 by default
                }
                else if (Cert.GetRSAPublicKey() is not null)
                {
                    resolvedAlg = Map.Jws["RS256"];   // RSA   → RS256 by default
                }
                else
                {
                    var keyType = "unknown";
                    if (Cert.PublicKey != null && Cert.PublicKey.EncodedKeyValue != null && Cert.PublicKey.EncodedKeyValue.Oid != null)
                    {
                        keyType = Cert.PublicKey.EncodedKeyValue.Oid.FriendlyName ?? "unknown";
                    }

                    throw new NotSupportedException(
                        $"Unsupported key type: {keyType}");
                }
            }

            return new SigningCredentials(key, resolvedAlg);
        }
    }

    // ── Encryption configs ────────────────────────────────────────────
    private abstract record BaseEnc(string KeyAlg, string EncAlg) : IEncConfig
    {
        protected string KeyAlgMapped => Map.KeyAlg[KeyAlg];
        protected string EncAlgMapped => Map.EncAlg[EncAlg];
        public abstract EncryptingCredentials ToEncryptingCreds();
    }

    private sealed record CertEncrypt(X509Certificate2 Cert, string KeyAlg, string EncAlg) : BaseEnc(KeyAlg, EncAlg)
    {
        public override EncryptingCredentials ToEncryptingCreds()
        {
            var key = new X509SecurityKey(Cert);
            return new EncryptingCredentials(key, KeyAlgMapped, EncAlgMapped);
        }
    }

    private sealed record RsaEncrypt(string Pem, string KeyAlg, string EncAlg) : BaseEnc(KeyAlg, EncAlg)
    {
        public override EncryptingCredentials ToEncryptingCreds()
        {
            var rsa = RSA.Create(); rsa.ImportFromPem(Pem);
            var key = new RsaSecurityKey(rsa);
            return new EncryptingCredentials(key, KeyAlgMapped, EncAlgMapped);
        }
    }

    private sealed record SymmetricEncrypt(
     string B64,
     string KeyAlg,
     string EncAlg) : BaseEnc(KeyAlg, EncAlg)
    {
        public override EncryptingCredentials ToEncryptingCreds()
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Encrypting with {KeyAlg} and {EncAlg} ({Bits} bits)",
                    KeyAlg, EncAlg, Base64UrlEncoder.DecodeBytes(B64).Length * 8);
            }
            // ────────── shared-secret → SymmetricSecurityKey ──────────
            if (!Map.KeyAlg.ContainsKey(KeyAlg))
            {
                throw new ArgumentException($"Unknown key algorithm: {KeyAlg}");
            }

            var key = new SymmetricSecurityKey(Base64UrlEncoder.DecodeBytes(B64));
            var bits = key.KeySize;                        // 128 / 192 / 256 / 384 / 512 …

            // ────────── auto-pick encAlg for 'dir' default case ───────
            var encEff = EncAlg;

            if (KeyAlg.Equals("dir", StringComparison.OrdinalIgnoreCase) &&
                EncAlg.Equals("A256CBC-HS512", StringComparison.OrdinalIgnoreCase))
            {
                encEff = bits switch
                {
                    128 => "A128GCM",
                    192 => "A192GCM",
                    256 => "A256GCM",
                    384 => "A192CBC-HS384",
                    512 => "A256CBC-HS512",
                    _ => throw new ArgumentException(
                               $"Unsupported key size {bits} bits for direct encryption.")
                };
            }

            // ────────── hard validation (caller may specify any enc) ──
            static void Require(int actualBits, int requiredBits, string alg)
            {
                _ = actualBits == requiredBits
                    ? true
                    : throw new ArgumentException($"{alg} requires a {requiredBits}-bit key.");
            }

            switch (encEff.ToUpperInvariant())
            {
                case "A128GCM": Require(bits, 128, encEff); break;
                case "A192GCM": Require(bits, 192, encEff); break;
                case "A256GCM": Require(bits, 256, encEff); break;
                case "A128CBC-HS256": Require(bits, 256, encEff); break;
                case "A192CBC-HS384": Require(bits, 384, encEff); break;
                case "A256CBC-HS512": Require(bits, 512, encEff); break;
                default:
                    throw new ArgumentException($"Unknown or unsupported enc algorithm: {encEff}");
            }
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Encrypting with {KeyAlg} and {EncAlg} ({Bits} bits)",
                    KeyAlg, encEff, bits);
            }
            // ────────── build EncryptingCredentials ───────────────────
            return new EncryptingCredentials(
                key,
                Map.KeyAlg[KeyAlg.ToUpperInvariant()],          // 'dir', 'A256KW', …
                Map.EncAlg[encEff.ToUpperInvariant()]);         // validated / auto-picked enc
        }
    }

    /// <summary>
    /// Renews a JWT token from the current request context, optionally extending its lifetime.
    /// </summary>
    /// <param name="ctx">The Kestrun context containing the request and authorization header.</param>
    /// <param name="lifetime">The new lifetime for the renewed token. If null, uses the builder's default lifetime.</param>

    /// <returns>The renewed JWT token as a compact string.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if no Bearer token is provided in the request.</exception>
    public string RenewJwt(
            KestrunContext ctx,
            TimeSpan? lifetime = null)
    {
        if (ctx.Request.Authorization == null || (!ctx.Request.Authorization?.StartsWith("Bearer ") ?? true))
        {
            return string.Empty;
        }
        var authHeader = ctx.Request.Authorization;
        var strToken = authHeader != null ? authHeader["Bearer ".Length..].Trim() : throw new UnauthorizedAccessException("No Bearer token provided");
        return RenewJwt(jwt: strToken, lifetime: lifetime);
    }

    /// <summary>
    /// Extends the validity period of an existing JWT token by creating a new token with updated lifetime.
    /// </summary>
    /// <param name="jwt">The original JWT token to extend.</param>
    /// <param name="lifetime">The new lifetime for the extended token. If null, uses the builder's default lifetime.</param>
    /// <returns>The extended JWT token as a compact string.</returns>
    public string RenewJwt(
        string jwt,
        TimeSpan? lifetime = null)
    {
        var handler = new JwtSecurityTokenHandler();

        // Read raw token (no mapping, no validation)
        var old = handler.ReadJwtToken(jwt);
        var _builder = CloneBuilder();
        // Copy all non-time claims
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "exp", "nbf", "iat"  };

        var claims = old.Claims.Where(c => !reserved.Contains(c.Type)).ToList();

        // If you rely on "sub", make sure it’s there (some libs put it into NameIdentifier)
        if (!claims.Any(c => c.Type == JwtRegisteredClaimNames.Sub))
        {
            var sub = old.Claims.FirstOrDefault(c =>
                         c.Type is JwtRegisteredClaimNames.Sub or
                         ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(sub))
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));
            }
        }

        var signCreds = BuildSigningCredentials(out _issuerSigningKey) ?? throw new InvalidOperationException("No signing credentials configured.");
        Algorithm = signCreds.Algorithm;
        var encCreds = BuildEncryptingCredentials();

        // Keep the same kid if present by setting it on the signing key
        // signing.Key.KeyId = old.Header.Kid; // uncomment if you must mirror the old 'kid'
        if (_nbf < DateTime.UtcNow)
        {
            _nbf = DateTime.UtcNow;
        }
        if (lifetime is null)
        {
            lifetime = _lifetime;
        }
        else if (lifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be a positive TimeSpan.");
        }
        var token = handler.CreateJwtSecurityToken(
                issuer: _issuer,
                audience: _aud,
                subject: new ClaimsIdentity(claims),
                notBefore: _nbf,
                expires: _nbf.Add((TimeSpan)lifetime),
                issuedAt: DateTime.UtcNow,
                signingCredentials: signCreds,
                encryptingCredentials: encCreds);

        foreach (var kv in _header)
        {
            token.Header[kv.Key] = kv.Value;
        }

        return handler.WriteToken(token);
    }
    /*
        public JwtTokenPackage BuildPackage()
        {
            string jwt = BuildToken(out var key);   // your existing overload

            var tvp = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,      // private fields in builder
                ValidateAudience = true,
                ValidAudience = _aud,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),

                RequireSignedTokens = key is not null,
                ValidateIssuerSigningKey = key is not null,
                IssuerSigningKey = key,
                ValidAlgorithms = key is not null
                    ? new[] { SecurityAlgorithms.HmacSha256 }
                    : Array.Empty<string>()
            };

            return new JwtTokenPackage(jwt, key, tvp);
        }
    */
}
