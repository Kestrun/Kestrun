using Kestrun.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Xunit;

namespace KestrunTests.Authentication;

public class OidcOptionsTests
{
    [Fact]
    [Trait("Category", "Authentication")]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var options = new OidcOptions();

        // Assert
        Assert.NotNull(options.CookieOptions);
        Assert.True(options.CookieOptions.SlidingExpiration);
        Assert.Null(options.JwkJson);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CookieScheme_DerivedFromCookieName()
    {
        // Arrange
        var options = new OidcOptions();
        options.CookieOptions.Cookie.Name = "TestOidcCookie";

        // Act
        var scheme = options.CookieScheme;

        // Assert
        Assert.Equal("TestOidcCookie", scheme);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CookieScheme_DefaultsWhenCookieNameNotSet()
    {
        // Arrange
        var options = new OidcOptions();

        // Act
        var scheme = options.CookieScheme;

        // Assert
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme + "." + options.AuthenticationScheme, scheme);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void JwkJson_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new OidcOptions();
        var jwkJson = /*lang=json,strict*/ "{\"kty\":\"RSA\",\"n\":\"test\",\"e\":\"AQAB\"}";

        // Act
        options.JwkJson = jwkJson;

        // Assert
        Assert.Equal(jwkJson, options.JwkJson);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_OidcOptions_CopiesAllProperties()
    {
        // Arrange
        var source = new OidcOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-secret",
            Authority = "https://example.com",
            ResponseType = "code",
            UsePkce = true,
            SaveTokens = true,
            JwkJson = /*lang=json,strict*/ "{\"kty\":\"RSA\"}"
        };
        source.Scope.Add("openid");
        source.Scope.Add("profile");

        var target = new OidcOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.Equal("test-client-id", target.ClientId);
        Assert.Equal("test-secret", target.ClientSecret);
        Assert.Equal("https://example.com", target.Authority);
        Assert.Equal("code", target.ResponseType);
        Assert.True(target.UsePkce);
        Assert.True(target.SaveTokens);
        Assert.Equal(/*lang=json,strict*/ "{\"kty\":\"RSA\"}", target.JwkJson);
        Assert.Contains("openid", target.Scope);
        Assert.Contains("profile", target.Scope);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_OpenIdConnectOptions_CopiesAllProperties()
    {
        // Arrange
        var source = new OidcOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-secret",
            Authority = "https://example.com",
            ResponseType = "code",
            UsePkce = true,
            SaveTokens = true,
            GetClaimsFromUserInfoEndpoint = true
        };
        source.Scope.Add("openid");
        source.Scope.Add("email");

        var target = new OpenIdConnectOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.Equal("test-client-id", target.ClientId);
        Assert.Equal("test-secret", target.ClientSecret);
        Assert.Equal("https://example.com", target.Authority);
        Assert.Equal("code", target.ResponseType);
        Assert.True(target.UsePkce);
        Assert.True(target.SaveTokens);
        Assert.True(target.GetClaimsFromUserInfoEndpoint);
        Assert.Contains("openid", target.Scope);
        Assert.Contains("email", target.Scope);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesTokenValidationParameters()
    {
        // Arrange
        var source = new OidcOptions();
        source.TokenValidationParameters.ValidateIssuer = false;
        source.TokenValidationParameters.ValidateAudience = true;

        var target = new OpenIdConnectOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.False(target.TokenValidationParameters.ValidateIssuer);
        Assert.True(target.TokenValidationParameters.ValidateAudience);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesClaimActions()
    {
        // Arrange
        var source = new OidcOptions();
        source.ClaimActions.MapJsonKey("email", "email");
        source.ClaimActions.MapJsonKey("name", "name");

        var target = new OpenIdConnectOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.NotEmpty(target.ClaimActions);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesEvents()
    {
        // Arrange
        var source = new OidcOptions
        {
            Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context => Task.CompletedTask
            }
        };

        var target = new OpenIdConnectOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.NotNull(target.Events);
        _ = Assert.IsType<OpenIdConnectEvents>(target.Events);
    }

#if NET9_0_OR_GREATER
    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesPushedAuthorizationBehavior_NET9()
    {
        // Arrange
        var source = new OidcOptions
        {
            PushedAuthorizationBehavior = PushedAuthorizationBehavior.Require
        };

        var target = new OpenIdConnectOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.Equal(PushedAuthorizationBehavior.Require, target.PushedAuthorizationBehavior);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_CopiesAdditionalAuthorizationParameters_NET9()
    {
        // Arrange
        var source = new OidcOptions();
        source.AdditionalAuthorizationParameters.Add("resource", "https://api.example.com");
        source.AdditionalAuthorizationParameters.Add("custom_param", "value");

        var target = new OpenIdConnectOptions();

        // Act
        source.ApplyTo(target);

        // Assert
        Assert.Contains("resource", target.AdditionalAuthorizationParameters.Keys);
        Assert.Equal("https://api.example.com", target.AdditionalAuthorizationParameters["resource"]);
        Assert.Contains("custom_param", target.AdditionalAuthorizationParameters.Keys);
        Assert.Equal("value", target.AdditionalAuthorizationParameters["custom_param"]);
    }
#endif

    [Fact]
    [Trait("Category", "Authentication")]
    public void ApplyTo_WithNullTarget_Throws()
    {
        // Arrange
        var source = new OidcOptions();

        // Act & Assert - test both overloads
        _ = Assert.ThrowsAny<Exception>(() => source.ApplyTo(null!));
        _ = Assert.ThrowsAny<Exception>(() => source.ApplyTo((OpenIdConnectOptions)null!));
    }
}
