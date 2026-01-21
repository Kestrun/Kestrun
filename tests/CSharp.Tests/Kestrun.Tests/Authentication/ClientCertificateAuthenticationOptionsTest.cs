using System.Security.Cryptography.X509Certificates;
using Kestrun.Authentication;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication.Certificate;
using Serilog;
using Xunit;

namespace KestrunTests.Authentication;

public class ClientCertificateAuthenticationOptionsTest
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var options = new ClientCertificateAuthenticationOptions();

        Assert.NotNull(options);
        Assert.Empty(options.DocumentationId);
        Assert.Null(options.DisplayName);
        Assert.Null(options.Description);
        Assert.False(options.Deprecated);
        Assert.False(options.GlobalScheme);
    }

    [Fact]
    public void AllowedCertificateTypes_HasDefaultValue()
    {
        var options = new ClientCertificateAuthenticationOptions();

        // ASP.NET Core default is Chained
        Assert.Equal(CertificateTypes.Chained, options.AllowedCertificateTypes);
    }

    [Fact]
    public void ValidateCertificateUse_HasDefaultValue()
    {
        var options = new ClientCertificateAuthenticationOptions();

        // ASP.NET Core default is true
        Assert.True(options.ValidateCertificateUse);
    }

    [Fact]
    public void ValidateValidityPeriod_HasDefaultValue()
    {
        var options = new ClientCertificateAuthenticationOptions();

        // ASP.NET Core default is true
        Assert.True(options.ValidateValidityPeriod);
    }

    [Fact]
    public void RevocationMode_HasDefaultValue()
    {
        var options = new ClientCertificateAuthenticationOptions();

        // ASP.NET Core default is Online
        Assert.Equal(X509RevocationMode.Online, options.RevocationMode);
    }

    [Fact]
    public void Logger_ReturnsHostLogger_WhenHostSet()
    {
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
        var host = new KestrunHost("Test", logger);
        var options = new ClientCertificateAuthenticationOptions
        {
            Host = host
        };

        Assert.Equal(host.Logger, options.Logger);
    }

    [Fact]
    public void Logger_CanBeSetExplicitly()
    {
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
        var options = new ClientCertificateAuthenticationOptions
        {
            Logger = logger
        };

        Assert.Equal(logger, options.Logger);
    }

    [Fact]
    public void ApplyTo_CopiesAllowedCertificateTypes()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            AllowedCertificateTypes = CertificateTypes.Chained
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.Equal(CertificateTypes.Chained, target.AllowedCertificateTypes);
    }

    [Fact]
    public void ApplyTo_CopiesValidateCertificateUse()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            ValidateCertificateUse = true
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.True(target.ValidateCertificateUse);
    }

    [Fact]
    public void ApplyTo_CopiesValidateValidityPeriod()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            ValidateValidityPeriod = true
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.True(target.ValidateValidityPeriod);
    }

    [Fact]
    public void ApplyTo_CopiesRevocationMode()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            RevocationMode = X509RevocationMode.Online
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.Equal(X509RevocationMode.Online, target.RevocationMode);
    }

    [Fact]
    public void ApplyTo_CopiesOpenApiProperties()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            DisplayName = "Test Display",
            Description = "Test Description",
            Deprecated = true,
            GlobalScheme = true,
            DocumentationId = ["doc1", "doc2"]
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.Equal("Test Display", target.DisplayName);
        Assert.Equal("Test Description", target.Description);
        Assert.True(target.Deprecated);
        Assert.True(target.GlobalScheme);
        Assert.Equal(source.DocumentationId, target.DocumentationId);
    }

    [Fact]
    public void ApplyTo_CopiesHost()
    {
        var host = new KestrunHost("Test", Log.Logger);
        var source = new ClientCertificateAuthenticationOptions
        {
            Host = host
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.Equal(host, target.Host);
    }

    [Fact]
    public void ApplyTo_CopiesChainTrustValidationMode()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.Equal(X509ChainTrustMode.CustomRootTrust, target.ChainTrustValidationMode);
    }

    [Fact]
    public void ApplyTo_CopiesRevocationFlag()
    {
        var source = new ClientCertificateAuthenticationOptions
        {
            RevocationFlag = X509RevocationFlag.EntireChain
        };
        var target = new ClientCertificateAuthenticationOptions();

        source.ApplyTo(target);

        Assert.Equal(X509RevocationFlag.EntireChain, target.RevocationFlag);
    }
}
