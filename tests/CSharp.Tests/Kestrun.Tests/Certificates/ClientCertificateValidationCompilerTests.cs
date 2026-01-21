using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kestrun.Certificates;
using Kestrun.Hosting;
using Kestrun.Scripting;
using Serilog;
using Xunit;

namespace KestrunTests.Certificates;

public sealed class ClientCertificateValidationCompilerTests
{
    private static X509Certificate2 CreateSelfSignedCertificate(string subject = "CN=Test")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    [Fact]
    public void Compile_Throws_WhenHostIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ClientCertificateValidationCompiler.Compile(null!, "return true; // host-null", ScriptLanguage.CSharp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Compile_Throws_WhenCodeIsBlank(string? code)
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ClientCertificateValidationCompiler.Compile(host, code!, ScriptLanguage.CSharp));
        Assert.Equal("code", ex.ParamName);
    }

    [Fact]
    public void Compile_Throws_WhenLanguageIsNotSupported()
    {
        using var host = new KestrunHost("Tests", Log.Logger);

        Assert.Throws<NotSupportedException>(() =>
            ClientCertificateValidationCompiler.Compile(host, "return true; // unsupported-lang", ScriptLanguage.PowerShell));
    }

    [Fact]
    public void Compile_CSharp_ReturnsDelegate_ThatExecutes()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var code = "return sslPolicyErrors == SslPolicyErrors.None; // cs-exec";

        var callback = ClientCertificateValidationCompiler.Compile(host, code, ScriptLanguage.CSharp);

        var cert = CreateSelfSignedCertificate();
        Assert.True(callback(cert, new X509Chain(), SslPolicyErrors.None));
        Assert.False(callback(cert, new X509Chain(), SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void Compile_VBNet_ReturnsDelegate_ThatExecutes()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var code = "Return sslPolicyErrors = SslPolicyErrors.None ' vb-exec";

        var callback = ClientCertificateValidationCompiler.Compile(host, code, ScriptLanguage.VBNet);

        var cert = CreateSelfSignedCertificate();
        Assert.True(callback(cert, new X509Chain(), SslPolicyErrors.None));
        Assert.False(callback(cert, new X509Chain(), SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void Compile_Caches_ByLanguageAndCode()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var code = "return true; // cache-test";

        var first = ClientCertificateValidationCompiler.Compile(host, code, ScriptLanguage.CSharp);
        var second = ClientCertificateValidationCompiler.Compile(host, code, ScriptLanguage.CSharp);

        Assert.Same(first, second);
    }

    [Fact]
    public void Compile_ThrowsCompilationErrorException_OnInvalidCode()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var code = "return doesNotExist; // compile-error";

        var ex = Assert.Throws<CompilationErrorException>(() =>
            ClientCertificateValidationCompiler.Compile(host, code, ScriptLanguage.CSharp));

        Assert.Contains("compilation failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(ex.Diagnostics);
    }
}
