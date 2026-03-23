using System.Reflection;
using Kestrun.Authentication;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Xunit;

namespace Kestrun.Tests.Authentication;

public class CookieAndWindowsAuthOptionsTests
{
    [Fact]
    public void CookieAuthOptions_ApplyToCookieAuthOptions_CopiesCoreValues()
    {
        var source = new CookieAuthOptions
        {
            LoginPath = "/login",
            LogoutPath = "/logout",
            AccessDeniedPath = "/denied",
            ReturnUrlParameter = "returnTo",
            ExpireTimeSpan = TimeSpan.FromMinutes(33),
            SlidingExpiration = true,
            ForwardDefault = "forward",
            ClaimsIssuer = "issuer",
            Description = "desc",
            DisplayName = "display",
            DocumentationId = ["cookie-auth"],
            GlobalScheme = true,
            Deprecated = true,
        };
        source.Cookie.Name = "kestrun";
        source.Cookie.HttpOnly = false;

        using var host = new KestrunHost("Tests", Log.Logger);
        source.Host = host;

        var target = new CookieAuthOptions();
        source.ApplyTo(target);

        Assert.Equal("/login", target.LoginPath);
        Assert.Equal("/logout", target.LogoutPath);
        Assert.Equal("/denied", target.AccessDeniedPath);
        Assert.Equal("returnTo", target.ReturnUrlParameter);
        Assert.Equal(TimeSpan.FromMinutes(33), target.ExpireTimeSpan);
        Assert.True(target.SlidingExpiration);
        Assert.Equal("forward", target.ForwardDefault);
        Assert.Equal("issuer", target.ClaimsIssuer);
        Assert.Equal("kestrun", target.Cookie.Name);
        Assert.False(target.Cookie.HttpOnly);

        Assert.Equal("desc", target.Description);
        Assert.Equal("display", target.DisplayName);
        Assert.Equal(["cookie-auth"], target.DocumentationId);
        Assert.True(target.GlobalScheme);
        Assert.True(target.Deprecated);
    }

    [Fact]
    public void CookieAuthOptions_ApplyToFrameworkOptions_CopiesCookieAndRoutingFields()
    {
        var source = new CookieAuthOptions
        {
            LoginPath = "/login",
            LogoutPath = "/logout",
            AccessDeniedPath = "/denied",
            ReturnUrlParameter = "returnTo",
            ExpireTimeSpan = TimeSpan.FromMinutes(5),
            SlidingExpiration = false,
            ForwardAuthenticate = "auth",
            ForwardChallenge = "challenge",
            ForwardForbid = "forbid",
            ForwardSignIn = "signin",
            ForwardSignOut = "signout",
            ClaimsIssuer = "issuer",
        };
        source.Cookie.Name = "auth-cookie";
        source.Cookie.Path = "/";

        var target = new CookieAuthenticationOptions();
        source.ApplyTo(target);

        Assert.Equal("/login", target.LoginPath);
        Assert.Equal("/logout", target.LogoutPath);
        Assert.Equal("/denied", target.AccessDeniedPath);
        Assert.Equal("returnTo", target.ReturnUrlParameter);
        Assert.Equal(TimeSpan.FromMinutes(5), target.ExpireTimeSpan);
        Assert.False(target.SlidingExpiration);
        Assert.Equal("auth", target.ForwardAuthenticate);
        Assert.Equal("challenge", target.ForwardChallenge);
        Assert.Equal("forbid", target.ForwardForbid);
        Assert.Equal("signin", target.ForwardSignIn);
        Assert.Equal("signout", target.ForwardSignOut);
        Assert.Equal("issuer", target.ClaimsIssuer);
        Assert.Equal("auth-cookie", target.Cookie.Name);
    }

    [Fact]
    public void CookieAuthOptions_Logger_UsesHostLoggerUntilOverridden()
    {
        var hostLogger = new LoggerConfiguration().CreateLogger();
        using var host = new KestrunHost("Tests", hostLogger);

        var options = new CookieAuthOptions { Host = host };
        Assert.Same(hostLogger, options.Logger);

        var overrideLogger = new LoggerConfiguration().CreateLogger();
        options.Logger = overrideLogger;
        Assert.Same(overrideLogger, options.Logger);
    }

    [Fact]
    public void WindowsAuthOptions_ApplyToWindowsAuthOptions_CopiesValues()
    {
        var source = new WindowsAuthOptions
        {
            PersistKerberosCredentials = true,
            PersistNtlmCredentials = true,
            Description = "desc",
            DisplayName = "display",
            DocumentationId = ["windows-auth"],
            GlobalScheme = true,
            Deprecated = true,
        };

        using var host = new KestrunHost("Tests", Log.Logger);
        source.Host = host;

        var target = new WindowsAuthOptions();
        source.ApplyTo(target);

        Assert.True(target.PersistKerberosCredentials);
        Assert.True(target.PersistNtlmCredentials);
        Assert.Equal("desc", target.Description);
        Assert.Equal("display", target.DisplayName);
        Assert.Equal(["windows-auth"], target.DocumentationId);
        Assert.True(target.GlobalScheme);
        Assert.True(target.Deprecated);
    }

    [Fact]
    public void WindowsAuthOptions_ApplyToNegotiateOptions_CopiesCredentialFlags()
    {
        var source = new WindowsAuthOptions
        {
            PersistKerberosCredentials = true,
            PersistNtlmCredentials = true,
        };

        var target = new NegotiateOptions();
        source.ApplyTo(target);

        Assert.True(target.PersistKerberosCredentials);
        Assert.True(target.PersistNtlmCredentials);
    }

    [Fact]
    public void WindowsAuthOptions_Logger_UsesHostLoggerUntilOverridden()
    {
        var hostLogger = new LoggerConfiguration().CreateLogger();
        using var host = new KestrunHost("Tests", hostLogger);

        var options = new WindowsAuthOptions { Host = host };
        Assert.Same(hostLogger, options.Logger);

        var overrideLogger = new LoggerConfiguration().CreateLogger();
        options.Logger = overrideLogger;
        Assert.Same(overrideLogger, options.Logger);
    }

    [Fact]
    public void AuthenticationBuilderExtensions_AddCookie_ConfiguresSchemeOptions()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuthentication();

        var extensionType = typeof(CookieAuthOptions).Assembly.GetType("Kestrun.Authentication.AuthenticationBuilderExtensions", throwOnError: true)!;
        var addCookie = extensionType.GetMethod(
            "AddCookie",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(AuthenticationBuilder),
                typeof(string),
                typeof(string),
                typeof(Action<CookieAuthOptions>)
            ],
            modifiers: null);

        Assert.NotNull(addCookie);

        _ = addCookie!.Invoke(null,
        [
            builder,
            "TestCookie",
            "Test Cookie",
            (Action<CookieAuthOptions>)(opts =>
            {
                opts.LoginPath = "/login";
                opts.Cookie.Name = "kestrun-cookie";
                opts.SlidingExpiration = true;
            })
        ]);

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var configured = monitor.Get("TestCookie");

        Assert.Equal("/login", configured.LoginPath);
        Assert.Equal("kestrun-cookie", configured.Cookie.Name);
        Assert.True(configured.SlidingExpiration);
    }
}
