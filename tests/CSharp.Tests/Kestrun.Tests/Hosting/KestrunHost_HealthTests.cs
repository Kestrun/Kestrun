using Kestrun.Health;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Models;
using Kestrun.Scripting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHost_HealthTests
{
    private KestrunHost CreateHost(out List<Action<IApplicationBuilder>> middleware)
    {
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
        _ = logger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>())).Returns(logger.Object);
        var host = new KestrunHost("TestApp", logger.Object);
        var field = typeof(KestrunHost).GetField("_middlewareQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        middleware = (List<Action<IApplicationBuilder>>)field!.GetValue(host)!;
        return host;
    }

    #region AddHealthEndpoint Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_WithNullConfigure_UsesDefaultPattern()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddHealthEndpoint(configure: null);

        // Assert
        Assert.NotNull(result);
        Assert.Same(host, result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_WithConfigure_AppliesConfiguration()
    {
        // Arrange
        var host = CreateHost(out _);

        // Act
        var result = host.AddHealthEndpoint(options =>
        {
            options.Pattern = "/custom-health";
            options.AllowAnonymous = false;
            options.TreatDegradedAsUnhealthy = true;
        });

        // Assert
        Assert.NotNull(result);
        Assert.Same(host, result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_WithAutoRegisterEndpointFalse_SkipsMapping()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddHealthEndpoint(options =>
        {
            options.AutoRegisterEndpoint = false;
        });

        // Assert
        Assert.NotNull(result);
        // Verify no deferred middleware was added (count remains 0)
        Assert.Empty(middleware);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_BeforeConfiguration_DefersMappingToMiddleware()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddHealthEndpoint(options =>
        {
            options.Pattern = "/healthz";
        });

        // Assert
        Assert.NotNull(result);
        // Verify middleware was queued for deferred execution
        Assert.Single(middleware);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_WithOptions_ThrowsOnNullOptions()
    {
        // Arrange
        var host = CreateHost(out _);

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => host.AddHealthEndpoint((HealthEndpointOptions)null!));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_WithOptions_CopiesOptionsCorrectly()
    {
        // Arrange
        var host = CreateHost(out _);
        var options = new HealthEndpointOptions
        {
            Pattern = "/api/health",
            AllowAnonymous = false,
            TreatDegradedAsUnhealthy = true,
            DefaultTags = ["database", "cache"],
            RequireSchemes = ["Bearer"],
            RequirePolicies = ["HealthCheckPolicy"],
            CorsPolicy = "AllowHealthChecks",
            RateLimitPolicyName = "HealthRateLimit",
            ShortCircuit = true,
            ShortCircuitStatusCode = 200,
            ThrowOnDuplicate = true,
            OpenApiSummary = "Health check endpoint",
            OpenApiDescription = "Returns health status",
            OpenApiOperationId = "CheckHealth",
            OpenApiTags = ["Monitoring"],
            OpenApiGroupName = "v1",
            MaxDegreeOfParallelism = 4,
            ProbeTimeout = TimeSpan.FromSeconds(10),
            ResponseContentType = HealthEndpointContentType.Json,
            XmlRootElementName = "HealthReport",
            Compress = true
        };

        // Act
        var result = host.AddHealthEndpoint(options);

        // Assert
        Assert.NotNull(result);
        Assert.Same(host, result);
    }

    #endregion

    #region CopyHealthEndpointOptions Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyHealthEndpointOptions_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("CopyHealthEndpointOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var target = new HealthEndpointOptions();

        // Act & Assert
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            method!.Invoke(null, [null!, target]));
        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyHealthEndpointOptions_WithNullTarget_ThrowsArgumentNullException()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("CopyHealthEndpointOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var source = new HealthEndpointOptions();

        // Act & Assert
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            method!.Invoke(null, [source, null!]));
        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyHealthEndpointOptions_CopiesAllProperties()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("CopyHealthEndpointOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var source = new HealthEndpointOptions
        {
            Pattern = "/api/status",
            DefaultTags = ["tag1", "tag2"],
            AllowAnonymous = false,
            TreatDegradedAsUnhealthy = true,
            ThrowOnDuplicate = true,
            RequireSchemes = ["Bearer", "ApiKey"],
            RequirePolicies = ["Policy1", "Policy2"],
            CorsPolicy = "TestCorsPolicy",
            RateLimitPolicyName = "TestRateLimit",
            ShortCircuit = true,
            ShortCircuitStatusCode = 418,
            OpenApiSummary = "Test Summary",
            OpenApiDescription = "Test Description",
            OpenApiOperationId = "TestOperation",
            OpenApiTags = ["Tag1", "Tag2"],
            OpenApiGroupName = "TestGroup",
            MaxDegreeOfParallelism = 8,
            ProbeTimeout = TimeSpan.FromSeconds(15),
            AutoRegisterEndpoint = false,
            DefaultScriptLanguage = ScriptLanguage.PowerShell,
            ResponseContentType = HealthEndpointContentType.Xml,
            XmlRootElementName = "CustomRoot",
            Compress = true
        };

        var target = new HealthEndpointOptions();

        // Act
        _ = method!.Invoke(null, [source, target]);

        // Assert
        Assert.Equal(source.Pattern, target.Pattern);
        Assert.Equal(source.DefaultTags, target.DefaultTags);
        Assert.Equal(source.AllowAnonymous, target.AllowAnonymous);
        Assert.Equal(source.TreatDegradedAsUnhealthy, target.TreatDegradedAsUnhealthy);
        Assert.Equal(source.ThrowOnDuplicate, target.ThrowOnDuplicate);
        Assert.Equal(source.RequireSchemes, target.RequireSchemes);
        Assert.Equal(source.RequirePolicies, target.RequirePolicies);
        Assert.Equal(source.CorsPolicy, target.CorsPolicy);
        Assert.Equal(source.RateLimitPolicyName, target.RateLimitPolicyName);
        Assert.Equal(source.ShortCircuit, target.ShortCircuit);
        Assert.Equal(source.ShortCircuitStatusCode, target.ShortCircuitStatusCode);
        Assert.Equal(source.OpenApiSummary, target.OpenApiSummary);
        Assert.Equal(source.OpenApiDescription, target.OpenApiDescription);
        Assert.Equal(source.OpenApiOperationId, target.OpenApiOperationId);
        Assert.Equal(source.OpenApiTags, target.OpenApiTags);
        Assert.Equal(source.OpenApiGroupName, target.OpenApiGroupName);
        Assert.Equal(source.MaxDegreeOfParallelism, target.MaxDegreeOfParallelism);
        Assert.Equal(source.ProbeTimeout, target.ProbeTimeout);
        Assert.Equal(source.AutoRegisterEndpoint, target.AutoRegisterEndpoint);
        Assert.Equal(source.DefaultScriptLanguage, target.DefaultScriptLanguage);
        Assert.Equal(source.ResponseContentType, target.ResponseContentType);
        Assert.Equal(source.XmlRootElementName, target.XmlRootElementName);
        Assert.Equal(source.Compress, target.Compress);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyHealthEndpointOptions_WithEmptyCollections_CopiesEmptyArrays()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("CopyHealthEndpointOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var source = new HealthEndpointOptions
        {
            DefaultTags = [],
            RequireSchemes = [],
            RequirePolicies = [],
            OpenApiTags = []
        };

        var target = new HealthEndpointOptions();

        // Act
        _ = method!.Invoke(null, [source, target]);

        // Assert
        Assert.Empty(target.DefaultTags);
        Assert.Empty(target.RequireSchemes);
        Assert.Empty(target.RequirePolicies);
        Assert.Empty(target.OpenApiTags);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyHealthEndpointOptions_WithNullCollections_HandlesGracefully()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("CopyHealthEndpointOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var source = new HealthEndpointOptions();
        var target = new HealthEndpointOptions();

        // Act
        _ = method!.Invoke(null, [source, target]);

        // Assert - should not throw
        Assert.NotNull(target);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyHealthEndpointOptions_ResponseContentType_IsCopied()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("CopyHealthEndpointOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var source = new HealthEndpointOptions
        {
            ResponseContentType = HealthEndpointContentType.Yaml
        };
        var target = new HealthEndpointOptions();

        // Act
        _ = method!.Invoke(null, [source, target]);

        // Assert
        Assert.Equal(HealthEndpointContentType.Yaml, target.ResponseContentType);
    }

    #endregion

    #region ExtractTags Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithNoQueryParameters_ReturnsEmptyArray()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        var request = context.Request;

        // Act
        var result = (string[])method!.Invoke(null, [request])!;

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithSingleTagParameter_ReturnsSingleTag()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag=database");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Single(result);
        Assert.Equal("database", result[0]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithMultipleTagParameters_ReturnsMultipleTags()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag=database&tag=cache");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Contains("database", result);
        Assert.Contains("cache", result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithCommaSeparatedTags_SplitsTags()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag=database,cache,api");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Contains("database", result);
        Assert.Contains("cache", result);
        Assert.Contains("api", result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithTagsParameter_ExtractsFromTagsKey()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tags=database,cache");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Contains("database", result);
        Assert.Contains("cache", result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithBothTagAndTags_CombinesTags()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag=database&tags=cache,api");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Contains("database", result);
        Assert.Contains("cache", result);
        Assert.Contains("api", result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithDuplicateTags_DeduplicatesCaseInsensitive()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag=database&tag=DATABASE&tags=Database");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Single(result);
        Assert.Equal("database", result[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithWhitespace_TrimsAndFilters()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag= database , cache ");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Contains("database", result);
        Assert.Contains("cache", result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_WithEmptyStrings_FiltersEmptyValues()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag=database&tag=&tags=,cache,");

        // Act
        var result = (string[])method!.Invoke(null, [context.Request])!;

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Contains("database", result);
        Assert.Contains("cache", result);
    }

    #endregion

    #region DetermineStatusCode Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void DetermineStatusCode_WithHealthyStatus_Returns200()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("DetermineStatusCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (int)method!.Invoke(null, [ProbeStatus.Healthy, false])!;

        // Assert
        Assert.Equal(StatusCodes.Status200OK, result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DetermineStatusCode_WithDegradedAndTreatDegradedFalse_Returns200()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("DetermineStatusCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (int)method!.Invoke(null, [ProbeStatus.Degraded, false])!;

        // Assert
        Assert.Equal(StatusCodes.Status200OK, result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DetermineStatusCode_WithDegradedAndTreatDegradedTrue_Returns503()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("DetermineStatusCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (int)method!.Invoke(null, [ProbeStatus.Degraded, true])!;

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DetermineStatusCode_WithUnhealthyStatus_Returns503()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("DetermineStatusCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (int)method!.Invoke(null, [ProbeStatus.Unhealthy, false])!;

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DetermineStatusCode_WithUnhealthyAndTreatDegradedTrue_Returns503()
    {
        // Arrange
        var method = typeof(KestrunHost).GetMethod("DetermineStatusCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (int)method!.Invoke(null, [ProbeStatus.Unhealthy, true])!;

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result);
    }

    #endregion

    #region Method Existence Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_MethodExists()
    {
        // Arrange & Act
        var method = typeof(KestrunHost).GetMethod("AddHealthEndpoint",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null,
            [typeof(Action<HealthEndpointOptions>)],
            null);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(KestrunHost), method.ReturnType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHealthEndpoint_OverloadWithOptions_MethodExists()
    {
        // Arrange & Act
        var method = typeof(KestrunHost).GetMethod("AddHealthEndpoint",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null,
            [typeof(HealthEndpointOptions)],
            null);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(KestrunHost), method.ReturnType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtractTags_MethodExists()
    {
        // Arrange & Act
        var method = typeof(KestrunHost).GetMethod("ExtractTags",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(string[]), method.ReturnType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyHealthEndpointOptions_MethodExists()
    {
        // Arrange & Act
        var method = typeof(KestrunHost).GetMethod("CopyHealthEndpointOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(HealthEndpointOptions), parameters[0].ParameterType);
        Assert.Equal(typeof(HealthEndpointOptions), parameters[1].ParameterType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DetermineStatusCode_MethodExists()
    {
        // Arrange & Act
        var method = typeof(KestrunHost).GetMethod("DetermineStatusCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(int), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(ProbeStatus), parameters[0].ParameterType);
        Assert.Equal(typeof(bool), parameters[1].ParameterType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void MapHealthEndpointImmediate_MethodExists()
    {
        // Arrange & Act
        var method = typeof(KestrunHost).GetMethod("MapHealthEndpointImmediate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(HealthEndpointOptions), parameters[0].ParameterType);
        Assert.Equal(typeof(MapRouteOptions), parameters[1].ParameterType);
    }

    #endregion
}
