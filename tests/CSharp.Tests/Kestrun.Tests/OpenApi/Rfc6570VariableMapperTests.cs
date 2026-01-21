using Kestrun.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace KestrunTests.OpenApi;

/// <summary>
/// Tests for the RFC 6570 variable mapper that converts ASP.NET route values to RFC 6570 variables.
/// </summary>
public sealed class Rfc6570VariableMapperTests
{
    /// <summary>
    /// Creates a test HttpContext with specified route values.
    /// </summary>
    private static HttpContext CreateContextWithRouteValues(Dictionary<string, object?>? routeValues = null)
    {
        var context = new DefaultHttpContext();
        
        if (routeValues != null)
        {
            foreach (var kvp in routeValues)
            {
                context.Request.RouteValues[kvp.Key] = kvp.Value;
            }
        }
        
        return context;
    }

    [Fact]
    public void TryBuildRfc6570Variables_WithNullContext_ReturnsFalse()
    {
        // Arrange
        HttpContext? context = null;
        var template = "/users/{id}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context!, template, out var variables);

        // Assert
        Assert.False(result);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_WithNullTemplate_ReturnsFalse()
    {
        // Arrange
        var context = CreateContextWithRouteValues();
        string? template = null;

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template!, out var variables);

        // Assert
        Assert.False(result);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_WithEmptyTemplate_ReturnsFalse()
    {
        // Arrange
        var context = CreateContextWithRouteValues();
        var template = "";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.False(result);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_WithNoRouteValues_ReturnsTrue()
    {
        // Arrange
        var context = CreateContextWithRouteValues();
        var template = "/users/{id}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.True(variables.ContainsKey("id"));
        Assert.Null(variables["id"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_SimpleParameter_ExtractsValue()
    {
        // Arrange
        var routeValues = new Dictionary<string, object?>
        {
            ["id"] = "123"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{id}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Equal("123", variables["id"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_MultipleParameters_ExtractsAllValues()
    {
        // Arrange
        var routeValues = new Dictionary<string, object?>
        {
            ["userId"] = "42",
            ["orderId"] = "999"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{userId}/orders/{orderId}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Equal(2, variables.Count);
        Assert.Equal("42", variables["userId"]);
        Assert.Equal("999", variables["orderId"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ReservedOperator_ExtractsValue()
    {
        // Arrange - {+path} is RFC 6570 reserved operator for multi-segment paths
        var routeValues = new Dictionary<string, object?>
        {
            ["path"] = "folder/subfolder/file.txt"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/files/{+path}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Equal("folder/subfolder/file.txt", variables["path"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_RegexConstraint_ExtractsValue()
    {
        // Arrange - {userId:[0-9]+} is a regex-constrained parameter
        var routeValues = new Dictionary<string, object?>
        {
            ["userId"] = "12345"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{userId:[0-9]+}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Equal("12345", variables["userId"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ComplexTemplate_ExtractsAllValues()
    {
        // Arrange - Mix of simple, reserved operator, and regex constraints
        var routeValues = new Dictionary<string, object?>
        {
            ["version"] = "1",
            ["userId"] = "42",
            ["path"] = "documents/report.pdf"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/api/v{version:[0-9]+}/users/{userId}/files/{+path}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Equal(3, variables.Count);
        Assert.Equal("1", variables["version"]);
        Assert.Equal("42", variables["userId"]);
        Assert.Equal("documents/report.pdf", variables["path"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_MissingRouteValue_SetsNull()
    {
        // Arrange - Route has 'id' but template expects both 'id' and 'name'
        var routeValues = new Dictionary<string, object?>
        {
            ["id"] = "123"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{id}/profile/{name}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Equal(2, variables.Count);
        Assert.Equal("123", variables["id"]);
        Assert.Null(variables["name"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_CaseInsensitiveParameterNames()
    {
        // Arrange - Route values may have different casing
        var routeValues = new Dictionary<string, object?>
        {
            ["UserId"] = "42"  // Capital U
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{userId}";  // lowercase u

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Equal("42", variables["userId"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_NumericRouteValue_PreservesType()
    {
        // Arrange - Some route values might be numeric types
        var routeValues = new Dictionary<string, object?>
        {
            ["id"] = 42  // Integer, not string
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{id}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Equal(42, variables["id"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_NullRouteValue_PreservesNull()
    {
        // Arrange
        var routeValues = new Dictionary<string, object?>
        {
            ["id"] = null
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{id}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Null(variables["id"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_TemplateWithNoParameters_ReturnsEmptyDict()
    {
        // Arrange
        var context = CreateContextWithRouteValues();
        var template = "/api/status";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ExtraRouteValues_AreIgnored()
    {
        // Arrange - Context has more route values than template needs
        var routeValues = new Dictionary<string, object?>
        {
            ["id"] = "123",
            ["extraParam"] = "ignored",
            ["anotherParam"] = "also-ignored"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/users/{id}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Equal("123", variables["id"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ComplexRegexConstraint_ExtractsParameterName()
    {
        // Arrange - More complex regex with character classes
        var routeValues = new Dictionary<string, object?>
        {
            ["slug"] = "my-blog-post"
        };
        var context = CreateContextWithRouteValues(routeValues);
        var template = "/blog/{slug:[a-z0-9-]+}";

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.Equal("my-blog-post", variables["slug"]);
    }

    [Theory]
    [InlineData("/users/{id}", "id")]
    [InlineData("/files/{+path}", "path")]
    [InlineData("/api/v{version:[0-9]+}", "version")]
    [InlineData("/posts/{slug:[a-z-]+}", "slug")]
    [InlineData("/items/{itemId:[0-9a-f]{8}}", "itemId")]
    public void TryBuildRfc6570Variables_VariousTemplateFormats_ExtractsCorrectParameterName(
        string template,
        string expectedParamName)
    {
        // Arrange
        var routeValues = new Dictionary<string, object?>
        {
            [expectedParamName] = "test-value"
        };
        var context = CreateContextWithRouteValues(routeValues);

        // Act
        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(context, template, out var variables);

        // Assert
        Assert.True(result);
        Assert.Single(variables);
        Assert.True(variables.ContainsKey(expectedParamName));
        Assert.Equal("test-value", variables[expectedParamName]);
    }
}
