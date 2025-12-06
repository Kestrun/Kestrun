using Kestrun.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Xunit;
using Kestrun.Scripting;
using Kestrun.Hosting;
using System.Text.Encodings.Web;

namespace KestrunTests.Authentication;

public class ApiKeyAuthHandlerTest
{
    private static ApiKeyAuthenticationOptions GetDefaultOptions(
        string expectedKey = "test-key",
        bool allowInsecureHttp = true,
        bool allowQueryStringFallback = false,
        string headerName = "X-Api-Key")
    {
        return new ApiKeyAuthenticationOptions
        {
            ApiKeyName = headerName,
            StaticApiKey = expectedKey,
            AllowInsecureHttp = allowInsecureHttp,
            AllowQueryStringFallback = allowQueryStringFallback,
            Host = new KestrunHost("Tests", new LoggerConfiguration().MinimumLevel.Error().CreateLogger()),
            AdditionalHeaderNames = [],
            EmitChallengeHeader = true,
            ChallengeHeaderFormat = ApiKeyChallengeFormat.HeaderOnly
        };
    }

    private static ApiKeyAuthHandler CreateHandler(
        ApiKeyAuthenticationOptions options,
        HttpContext context)
    {
        var optionsMonitorMock = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        _ = optionsMonitorMock.Setup(m => m.CurrentValue).Returns(options);
        _ = optionsMonitorMock.Setup(m => m.Get(It.IsAny<string>())).Returns(options);
        var optionsMonitor = optionsMonitorMock.Object;
        var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
        var encoder = UrlEncoder.Default;
        var host = new KestrunHost("Tests", Log.Logger);
        var handler = new ApiKeyAuthHandler(host, optionsMonitor, loggerFactory, encoder);
        handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthHandler)),
            context
        ).GetAwaiter().GetResult();
        return handler;
    }

    private static HttpContext CreateHttpContext(
        string? apiKey = null,
        string headerName = "X-Api-Key",
        bool isHttps = true,
        bool addQueryString = false)
    {
        var context = new DefaultHttpContext();
        if (context.RequestServices is null)
        {
            var services = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            context.RequestServices = services;
        }
        context.Request.IsHttps = isHttps;
        if (apiKey != null)
        {
            context.Request.Headers[headerName] = apiKey;
        }
        if (addQueryString && apiKey != null)
        {
            context.Request.QueryString = new QueryString($"?{headerName}={apiKey}");
        }
        return context;
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Succeeds_WithValidApiKeyHeader()
    {
        var options = GetDefaultOptions();
        var context = CreateHttpContext("test-key");
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Fails_WithInvalidApiKey()
    {
        var options = GetDefaultOptions();
        var context = CreateHttpContext("wrong-key");
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Equal("Invalid API Key: wrong-key", result.Failure.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Fails_WhenApiKeyMissing()
    {
        var options = GetDefaultOptions();
        var context = CreateHttpContext("");
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Equal("Missing API Key", result.Failure.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Fails_WhenHttpsRequired_AndNotHttps()
    {
        var options = GetDefaultOptions(allowInsecureHttp: false);
        var context = CreateHttpContext("test-key", isHttps: false);
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Equal("HTTPS required", result.Failure.Message);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_Succeeds_WithQueryStringFallback()
    {
        var options = GetDefaultOptions(allowQueryStringFallback: true);
        var context = CreateHttpContext(apiKey: "test-key", addQueryString: true);
        var handler = CreateHandler(options, context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
    }

    [Fact]
    public async Task HandleChallengeAsync_SetsWwwAuthenticateHeader_And401()
    {
        var options = GetDefaultOptions();
        var context = CreateHttpContext();
        var handler = CreateHandler(options, context);

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.Equal(401, context.Response.StatusCode);
        Assert.True(context.Response.Headers.ContainsKey("WWW-Authenticate"));
    }

    [Fact]
    public void BuildPsValidator_ReturnsDelegate()
    {
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
        var host = new KestrunHost("Tests", logger);
        var settings = new AuthenticationCodeSettings { Code = "param($providedKey) return $providedKey -eq 'abc'", Language = ScriptLanguage.PowerShell };
        var validator = ApiKeyAuthHandler.BuildPsValidator(host, settings);

        Assert.NotNull(validator);
    }

    /*      [Fact]
          public void BuildCsValidator_ReturnsDelegate()
          {
              var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
              var settings = new AuthenticationCodeSettings
              {
                  Code = "return true;",
                  Language = Scripting.ScriptLanguage.CSharp,
                  CSharpVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest
              };
              var validator = ApiKeyAuthHandler.BuildCsValidator(settings, logger);

              Assert.NotNull(validator);
          }*/

    [Fact]
    public void BuildVBNetValidator_ReturnsDelegate()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("Tests", logger);
        var settings = new AuthenticationCodeSettings { Code = "providedKey = \"abc\"", Language = ScriptLanguage.VBNet };
        var validator = ApiKeyAuthHandler.BuildVBNetValidator(host, settings);

        Assert.NotNull(validator);
    }
}
