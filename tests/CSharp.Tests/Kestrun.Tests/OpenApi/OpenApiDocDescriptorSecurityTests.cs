using Kestrun.Authentication;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorSecurityTests
{
    [Fact]
    public void ApplySecurityScheme_AddsClientCertificateScheme_WithVendorExtensions()
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

        Assert.Equal(SecuritySchemeType.ApiKey, scheme.Type);
        Assert.Equal("mTLS", scheme.Name);
        Assert.Equal(ParameterLocation.Header, scheme.In);

        Assert.NotNull(scheme.Extensions);
        Assert.True(scheme.Extensions.ContainsKey("x-mtls"));
        Assert.True(scheme.Extensions.ContainsKey("x-transport-auth"));
        Assert.Contains("Mutual TLS", scheme.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
}
