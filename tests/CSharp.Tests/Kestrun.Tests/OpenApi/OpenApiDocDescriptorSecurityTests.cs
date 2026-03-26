using Kestrun.Authentication;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace Kestrun.Tests.OpenApi;

public sealed class OpenApiDocDescriptorSecurityTests
{
    [Fact]
    public void ApplySecurityScheme_AddsClientCertificateScheme()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var options = new ClientCertificateAuthenticationOptions
        {
            GlobalScheme = false,
            Deprecated = false,
            Description = null
        };

        descriptor.ApplySecurityScheme("Certificate", options);

        Assert.NotNull(descriptor.Document.Components);
        Assert.NotNull(descriptor.Document.Components.SecuritySchemes);
        Assert.True(descriptor.Document.Components.SecuritySchemes.TryGetValue("Certificate", out var scheme));
        Assert.NotNull(scheme);

        // Newer Microsoft.OpenApi versions model client certificate auth as 'mutualTLS'
        // (rather than an ApiKey/header scheme).
        Assert.Equal(SecuritySchemeType.MutualTLS, scheme.Type);

        // Kestrun no longer emits vendor extensions for the mutualTLS scheme.
        Assert.True(scheme.Extensions is null || scheme.Extensions.Count == 0);
    }

    [Fact]
    public void ApplySecurityScheme_AddsGlobalRequirement_WhenGlobalSchemeEnabled()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var options = new ClientCertificateAuthenticationOptions
        {
            GlobalScheme = true,
            Deprecated = false,
            Description = "Custom description"
        };

        descriptor.ApplySecurityScheme("Certificate", options);

        Assert.NotNull(descriptor.Document.Security);
        Assert.NotEmpty(descriptor.Document.Security);
    }

    [Fact]
    public void ApplySecurityScheme_OAuth2_IncludesOAuth2MetadataUrl()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var options = new OAuth2Options
        {
            AuthorizationEndpoint = "https://issuer.example/authorize",
            TokenEndpoint = "https://issuer.example/token",
            OAuth2MetadataUrl = "https://issuer.example/.well-known/oauth-authorization-server"
        };

        descriptor.ApplySecurityScheme("OAuth2", options);

        Assert.NotNull(descriptor.Document.Components);
        Assert.NotNull(descriptor.Document.Components.SecuritySchemes);
        Assert.True(descriptor.Document.Components.SecuritySchemes.TryGetValue("OAuth2", out var scheme));
        Assert.NotNull(scheme);
        Assert.Equal(SecuritySchemeType.OAuth2, scheme.Type);
        var metadataProvider = Assert.IsAssignableFrom<IOAuth2MetadataProvider>(scheme);
        Assert.Equal(new Uri("https://issuer.example/.well-known/oauth-authorization-server"), metadataProvider.OAuth2MetadataUrl);
    }
}
