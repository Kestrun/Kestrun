using Kestrun.OpenApi;
using Microsoft.AspNetCore.Http;
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
    public void TryBuildRfc6570Variables_WithNullContext_ReturnsFalseAndError()
    {
        HttpContext? context = null;

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context!,
            "/users/{id}",
            out var variables,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Empty(variables);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryBuildRfc6570Variables_WithInvalidTemplate_ReturnsFalseAndError(string? template)
    {
        var context = CreateContextWithRouteValues();

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            template!,
            out var variables,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_TemplateWithNoParameters_ReturnsTrueEmptyDict()
    {
        var context = CreateContextWithRouteValues();

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/api/status",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_SimpleParameter_ExtractsValue()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?> { ["id"] = "123" });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{id}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("123", variables["id"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_MultipleParameters_ExtractsAllValues()
    {
        var routeValues = new Dictionary<string, object?>
        {
            ["userId"] = "42",
            ["orderId"] = "999",
        };
        var context = CreateContextWithRouteValues(routeValues);

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{userId}/orders/{orderId}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(2, variables.Count);
        Assert.Equal("42", variables["userId"]);
        Assert.Equal("999", variables["orderId"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ReservedOperator_AllowsMultiSegmentValue()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?>
        {
            ["path"] = "folder/subfolder/file.txt",
        });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/files/{+path}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("folder/subfolder/file.txt", variables["path"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ReservedOperator_LeadingSlashIsTrimmed()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?>
        {
            ["path"] = "/a/b",
        });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/files/{+path}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("a/b", variables["path"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ExplodeModifier_AllowsMultiSegmentValue()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?>
        {
            ["path"] = "a/b/c.txt",
        });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/files/{path*}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("a/b/c.txt", variables["path"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ExplodeModifier_LeadingSlashIsTrimmed()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?>
        {
            ["path"] = "/a/b",
        });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/files/{path*}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("a/b", variables["path"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_MissingRouteValue_ReturnsFalseAndError()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?> { ["id"] = "123" });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{id}/profile/{name}",
            out var variables,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_SlashInNonMultiSegmentValue_ReturnsFalseAndError()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?> { ["id"] = "a/b" });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{id}",
            out var variables,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_CaseInsensitiveRouteKeys_AreResolved()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?> { ["UserId"] = "42" });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{userId}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("42", variables["userId"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_NumericRouteValue_IsConvertedToInvariantString()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?> { ["id"] = 42 });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{id}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("42", variables["id"]);
    }

    [Fact]
    public void TryBuildRfc6570Variables_NullRouteValue_IsRejected()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?> { ["id"] = null });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{id}",
            out var variables,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_ExtraRouteValues_AreIgnored()
    {
        var routeValues = new Dictionary<string, object?>
        {
            ["id"] = "123",
            ["extraParam"] = "ignored",
        };
        var context = CreateContextWithRouteValues(routeValues);

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/users/{id}",
            out var variables,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        _ = Assert.Single(variables);
        Assert.Equal("123", variables["id"]);
    }

    [Theory]
    [InlineData("/users/{id:[0-9]+}")]
    [InlineData("/api/v{version:.*}")]
    public void TryBuildRfc6570Variables_ColonSyntax_IsRejected(string template)
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?>
        {
            ["id"] = "123",
            ["version"] = "1",
        });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            template,
            out var variables,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryBuildRfc6570Variables_MultipleVarsInOneExpression_IsRejected()
    {
        var context = CreateContextWithRouteValues(new Dictionary<string, object?>
        {
            ["a"] = "1",
            ["b"] = "2",
        });

        var result = Rfc6570VariableMapper.TryBuildRfc6570Variables(
            context,
            "/x/{a,b}",
            out var variables,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Empty(variables);
    }
}
