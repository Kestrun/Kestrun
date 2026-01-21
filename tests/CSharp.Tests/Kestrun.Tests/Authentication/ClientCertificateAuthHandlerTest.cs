using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using Kestrun.Authentication;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Xunit;

namespace KestrunTests.Authentication;

public class ClientCertificateAuthHandlerTest
{
    private static ClientCertificateAuthenticationOptions CreateOptions(
        CertificateTypes allowedTypes = CertificateTypes.All,
        bool validateUse = false,
        bool validateValidityPeriod = false,
        X509RevocationMode revocationMode = X509RevocationMode.NoCheck,
        Serilog.ILogger? logger = null)
    {
        return new ClientCertificateAuthenticationOptions
        {
            AllowedCertificateTypes = allowedTypes,
            ValidateCertificateUse = validateUse,
            ValidateValidityPeriod = validateValidityPeriod,
            RevocationMode = revocationMode,
            Host = new KestrunHost("Tests", logger ?? new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger())
        };
    }

    private static ClientCertificateAuthHandler CreateHandler(
        ClientCertificateAuthenticationOptions? options = null,
        HttpContext? context = null,
        X509Certificate2? certificate = null)
    {
        var effectiveOptions = options ?? CreateOptions();
        var optMonitorMock = new Mock<IOptionsMonitor<ClientCertificateAuthenticationOptions>>();
        _ = optMonitorMock.Setup(m => m.CurrentValue).Returns(effectiveOptions);
        _ = optMonitorMock.Setup(m => m.Get(It.IsAny<string>())).Returns(effectiveOptions);
        var optMonitor = optMonitorMock.Object;
        var loggerFactory = new LoggerFactory();
        var encoder = UrlEncoder.Default;

        var host = new KestrunHost("Tests", Log.Logger);
        var handler = new ClientCertificateAuthHandler(host, optMonitor, loggerFactory, encoder);
        var scheme = new AuthenticationScheme("Certificate", "Certificate", typeof(ClientCertificateAuthHandler));
        var ctx = context ?? new DefaultHttpContext();

        // Mock the certificate on the connection
        if (certificate != null)
        {
            var connectionMock = new Mock<ConnectionInfo>();
            _ = connectionMock.Setup(c => c.GetClientCertificateAsync(default))
                .ReturnsAsync(certificate);

            var contextMock = new Mock<HttpContext>();
            _ = contextMock.Setup(c => c.Connection).Returns(connectionMock.Object);
            _ = contextMock.Setup(c => c.RequestServices).Returns(ctx.RequestServices);
            _ = contextMock.Setup(c => c.Items).Returns(ctx.Items);
            _ = contextMock.Setup(c => c.Request).Returns(ctx.Request);
            _ = contextMock.Setup(c => c.Response).Returns(ctx.Response);
            _ = contextMock.Setup(c => c.User).Returns(ctx.User);

            ctx = contextMock.Object;
        }

        if (ctx.RequestServices is null)
        {
            var services = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            ctx.RequestServices = services;
        }

        handler.InitializeAsync(scheme, ctx).GetAwaiter().GetResult();
        return handler;
    }

    private static X509Certificate2 CreateSelfSignedCertificate(
        string subject = "CN=Test",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        bool includeClientAuth = true)
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add enhanced key usage extension for client authentication if requested
        if (includeClientAuth)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    [
                        new Oid("1.3.6.1.5.5.7.3.2") // Client Authentication
                    ],
                    false));
        }

        var cert = request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(365));

        return cert;
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsNoResult_WhenNoCertificateProvided()
    {
        var options = CreateOptions();
        var context = new DefaultHttpContext();
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Succeeds_WithValidSelfSignedCertificate()
    {
        var cert = CreateSelfSignedCertificate();
        var options = CreateOptions(allowedTypes: CertificateTypes.SelfSigned);
        var handler = CreateHandler(options, certificate: cert);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.True(result.Principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Fails_WhenSelfSignedNotAllowed()
    {
        var cert = CreateSelfSignedCertificate();
        var options = CreateOptions(allowedTypes: CertificateTypes.Chained);
        var handler = CreateHandler(options, certificate: cert);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("SelfSigned", result.Failure?.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Fails_WhenCertificateExpired()
    {
        var cert = CreateSelfSignedCertificate(
            notBefore: DateTimeOffset.UtcNow.AddDays(-365),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        var options = CreateOptions(validateValidityPeriod: true);
        var handler = CreateHandler(options, certificate: cert);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("validity period", result.Failure?.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Fails_WhenCertificateNotYetValid()
    {
        var cert = CreateSelfSignedCertificate(
            notBefore: DateTimeOffset.UtcNow.AddDays(1),
            notAfter: DateTimeOffset.UtcNow.AddDays(365));
        var options = CreateOptions(validateValidityPeriod: true);
        var handler = CreateHandler(options, certificate: cert);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("validity period", result.Failure?.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Fails_WhenClientAuthUsageMissing()
    {
        var cert = CreateSelfSignedCertificate(includeClientAuth: false);
        var options = CreateOptions(validateUse: true);
        var handler = CreateHandler(options, certificate: cert);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("Client Authentication", result.Failure?.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_CreatesClaims_WithCertificateInfo()
    {
        var cert = CreateSelfSignedCertificate("CN=TestUser");
        var options = CreateOptions();
        var handler = CreateHandler(options, certificate: cert);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var claims = result.Principal?.Claims.ToList();
        Assert.NotNull(claims);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name);
        Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier);
        Assert.Contains(claims, c => c.Type == "thumbprint" && c.Value == cert.Thumbprint);
        Assert.Contains(claims, c => c.Type == "issuer" && c.Value == cert.Issuer);
        Assert.Contains(claims, c => c.Type == "serialnumber");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_UsesSubjectAsName()
    {
        var subject = "CN=TestUser, O=TestOrg";
        var cert = CreateSelfSignedCertificate(subject);
        var options = CreateOptions();
        var handler = CreateHandler(options, certificate: cert);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var nameClaim = result.Principal?.FindFirst(ClaimTypes.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal(subject, nameClaim.Value);
    }

    [Fact]
    public void Options_ApplyTo_CopiesAllProperties()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            AllowedCertificateTypes = CertificateTypes.Chained,
            ValidateCertificateUse = true,
            ValidateValidityPeriod = true,
            RevocationMode = X509RevocationMode.Online,
            Description = "Test Description",
            Deprecated = true,
            Host = new KestrunHost("Test", Log.Logger)
        };

        var target = new ClientCertificateAuthenticationOptions();
        source.ApplyTo(target);

        Assert.Equal(source.AllowedCertificateTypes, target.AllowedCertificateTypes);
        Assert.Equal(source.ValidateCertificateUse, target.ValidateCertificateUse);
        Assert.Equal(source.ValidateValidityPeriod, target.ValidateValidityPeriod);
        Assert.Equal(source.RevocationMode, target.RevocationMode);
        Assert.Equal(source.Description, target.Description);
        Assert.Equal(source.Deprecated, target.Deprecated);
        Assert.Equal(source.Host, target.Host);
    }
}
