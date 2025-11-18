using Kestrun.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace KestrunTests.Authentication;

public class OidcEventsTests
{
    private static string CreateTestJwk()
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(true);
        var jwk = new
        {
            kty = "RSA",
            n = Base64UrlEncoder.Encode(p.Modulus),
            e = Base64UrlEncoder.Encode(p.Exponent),
            d = Base64UrlEncoder.Encode(p.D),
            p = Base64UrlEncoder.Encode(p.P),
            q = Base64UrlEncoder.Encode(p.Q),
            dp = Base64UrlEncoder.Encode(p.DP),
            dq = Base64UrlEncoder.Encode(p.DQ),
            qi = Base64UrlEncoder.Encode(p.InverseQ)
        };
        return JsonSerializer.Serialize(jwk, Kestrun.Certificates.JwkJson.Options);
    }

    private static AuthorizationCodeReceivedContext BuildContext(OpenIdConnectOptions options)
    {
        var http = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("oidc", "oidc", typeof(OpenIdConnectHandler));
        var props = new AuthenticationProperties();
        var ctx = new AuthorizationCodeReceivedContext(http, scheme, options, props)
        {
            TokenEndpointRequest = new OpenIdConnectMessage()
        };
        return ctx;
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public async Task AuthorizationCodeReceived_SetsClientAssertionFields()
    {
        // Arrange
        var jwk = CreateTestJwk();
        var service = new AssertionService("client-id", jwk);
        var events = new OidcEvents(service);

        var options = new OpenIdConnectOptions
        {
            Authority = "https://issuer.example",
            Configuration = new OpenIdConnectConfiguration
            {
                TokenEndpoint = "https://issuer.example/connect/token"
            }
        };

        var context = BuildContext(options);

        // Act
        await events.AuthorizationCodeReceived(context);

        // Assert
        Assert.Equal("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", context.TokenEndpointRequest!.ClientAssertionType);
        Assert.False(string.IsNullOrEmpty(context.TokenEndpointRequest!.ClientAssertion));
        var parts = context.TokenEndpointRequest!.ClientAssertion.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public async Task AuthorizationCodeReceived_NoRequest_DoesNothing()
    {
        var jwk = CreateTestJwk();
        var service = new AssertionService("client-id", jwk);
        var events = new OidcEvents(service);
        var options = new OpenIdConnectOptions { Authority = "https://issuer.example" };
        var http = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("oidc", "oidc", typeof(OpenIdConnectHandler));
        var props = new AuthenticationProperties();
        var ctx = new AuthorizationCodeReceivedContext(http, scheme, options, props)
        {
            TokenEndpointRequest = null
        };

        await events.AuthorizationCodeReceived(ctx);

        Assert.Null(ctx.TokenEndpointRequest);
    }
}
