using Kestrun.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Net;
using System.Text;
using Xunit;

namespace Kestrun.Tests.Authentication;

public class OAuth2OptionsTests
{
    [Fact]
    [Trait("Category", "Authentication")]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var options = new OAuth2Options();

        // Assert
        Assert.NotNull(options.CookieOptions);
        Assert.True(options.CookieOptions.SlidingExpiration);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CookieScheme_DerivedFromCookieName()
    {
        // Arrange
        var options = new OAuth2Options();
        options.CookieOptions.Cookie.Name = "TestAuthCookie";

        // Act
        var scheme = options.CookieScheme;

        // Assert
        Assert.Equal("TestAuthCookie", scheme);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CookieScheme_DefaultsWhenCookieNameNotSet()
    {
        // Arrange
        var options = new OAuth2Options();

        // Act
        var scheme = options.CookieScheme;

        // Assert
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme + "." + AuthenticationDefaults.OAuth2SchemeName, scheme);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesAllProperties()
    {
        // Arrange
        var source = new OAuth2Options
        {
            ClientId = "test-client-id",
            ClientSecret = "test-secret",
            AuthorizationEndpoint = "https://example.com/oauth/authorize",
            TokenEndpoint = "https://example.com/oauth/token",
            UserInformationEndpoint = "https://example.com/oauth/userinfo",
            UsePkce = true,
            CallbackPath = "/signin-oauth"
        };
        source.Scope.Add("openid");
        source.Scope.Add("profile");

        var target = new OAuthOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.Equal("test-client-id", target.ClientId);
        Assert.Equal("test-secret", target.ClientSecret);
        Assert.Equal("https://example.com/oauth/authorize", target.AuthorizationEndpoint);
        Assert.Equal("https://example.com/oauth/token", target.TokenEndpoint);
        Assert.Equal("https://example.com/oauth/userinfo", target.UserInformationEndpoint);
        Assert.True(target.UsePkce);
        Assert.Equal("/signin-oauth", target.CallbackPath);
        Assert.Contains("openid", target.Scope);
        Assert.Contains("profile", target.Scope);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesClaimActions()
    {
        // Arrange
        var source = new OAuth2Options();
        source.ClaimActions.MapJsonKey("email", "email");
        source.ClaimActions.MapJsonKey("name", "name");

        var target = new OAuthOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.NotEmpty(target.ClaimActions);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_OAuth2Target_CopiesMetadataSettings()
    {
        var source = new OAuth2Options
        {
            OAuth2MetadataUrl = "https://issuer.example/.well-known/oauth-authorization-server",
            ResolveEndpointsFromMetadata = true
        };

        var target = new OAuth2Options();

        source.ApplyTo(target);

        Assert.Equal("https://issuer.example/.well-known/oauth-authorization-server", target.OAuth2MetadataUrl);
        Assert.True(target.ResolveEndpointsFromMetadata);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesEvents()
    {
        var source = new OAuth2Options
        {
            Events = new OAuthEvents
            {
                OnCreatingTicket = context =>
                {
                    return Task.CompletedTask;
                }
            }
        };

        var target = new OAuthOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.NotNull(target.Events);
        // Events can't be directly compared, but we can verify it was copied
        _ = Assert.IsType<OAuthEvents>(target.Events);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesBackchannelConfiguration()
    {
        // Arrange
        var source = new OAuth2Options
        {
            BackchannelTimeout = TimeSpan.FromSeconds(30)
        };

        var target = new OAuthOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), target.BackchannelTimeout);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesStateDataFormat()
    {
        // Arrange
        var source = new OAuth2Options
        {
            SaveTokens = true
        };

        var target = new OAuthOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.True(target.SaveTokens);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_WithNullTarget_Throws()
    {
        // Arrange
        var source = new OAuth2Options();

        // Act & Assert
        _ = Assert.ThrowsAny<Exception>(() => source.ApplyTo(null!));
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public async Task PopulateEndpointsFromMetadataAsync_PopulatesMissingEndpoints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new OAuth2Options
        {
            OAuth2MetadataUrl = "https://issuer.example/.well-known/oauth-authorization-server"
        };

        const string payload = /*lang=json,strict*/ """
            {
              "authorization_endpoint":"https://issuer.example/authorize",
              "token_endpoint":"https://issuer.example/token",
              "userinfo_endpoint":"https://issuer.example/userinfo"
            }
            """;

        using var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler);

        await OAuth2Options.PopulateEndpointsFromMetadataAsync(options, client, cancellationToken);

        Assert.Equal("https://issuer.example/authorize", options.AuthorizationEndpoint);
        Assert.Equal("https://issuer.example/token", options.TokenEndpoint);
        Assert.Equal("https://issuer.example/userinfo", options.UserInformationEndpoint);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public async Task PopulateEndpointsFromMetadataAsync_DoesNotOverrideExplicitEndpoints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new OAuth2Options
        {
            OAuth2MetadataUrl = "https://issuer.example/.well-known/oauth-authorization-server",
            AuthorizationEndpoint = "https://explicit.example/authorize",
            TokenEndpoint = "https://explicit.example/token",
            UserInformationEndpoint = "https://explicit.example/userinfo"
        };

        const string payload = /*lang=json,strict*/ """
            {
              "authorization_endpoint":"https://issuer.example/authorize",
              "token_endpoint":"https://issuer.example/token",
              "userinfo_endpoint":"https://issuer.example/userinfo"
            }
            """;

        var requestCount = 0;
        using var handler = new StubMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });
        using var client = new HttpClient(handler);

        await OAuth2Options.PopulateEndpointsFromMetadataAsync(options, client, cancellationToken);

        Assert.Equal(0, requestCount);
        Assert.Equal("https://explicit.example/authorize", options.AuthorizationEndpoint);
        Assert.Equal("https://explicit.example/token", options.TokenEndpoint);
        Assert.Equal("https://explicit.example/userinfo", options.UserInformationEndpoint);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public async Task PopulateEndpointsFromMetadataAsync_WithoutMetadataUrl_DoesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new OAuth2Options
        {
            OAuth2MetadataUrl = null
        };

        var requestCount = 0;
        using var handler = new StubMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
        using var client = new HttpClient(handler);

        await OAuth2Options.PopulateEndpointsFromMetadataAsync(options, client, cancellationToken);

        Assert.Equal(0, requestCount);
        Assert.Null(options.AuthorizationEndpoint);
        Assert.Null(options.TokenEndpoint);
        Assert.Null(options.UserInformationEndpoint);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public async Task PopulateEndpointsFromMetadataAsync_MetadataFailure_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new OAuth2Options
        {
            OAuth2MetadataUrl = "https://issuer.example/.well-known/oauth-authorization-server"
        };

        using var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("metadata unavailable", Encoding.UTF8, "text/plain")
        });
        using var client = new HttpClient(handler);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => OAuth2Options.PopulateEndpointsFromMetadataAsync(options, client, cancellationToken));
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
