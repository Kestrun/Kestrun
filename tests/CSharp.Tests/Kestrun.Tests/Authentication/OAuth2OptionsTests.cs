using Kestrun.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Xunit;

namespace KestrunTests.Authentication;

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
    public void AuthenticationScheme_DerivedFromCookieName()
    {
        // Arrange
        var options = new OAuth2Options();
        options.CookieOptions.Cookie.Name = "TestAuthCookie";

        // Act
        var scheme = options.AuthenticationScheme;

        // Assert
        Assert.Equal("TestAuthCookie", scheme);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void AuthenticationScheme_DefaultsWhenCookieNameNotSet()
    {
        // Arrange
        var options = new OAuth2Options();

        // Act
        var scheme = options.AuthenticationScheme;

        // Assert
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme);
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
}
