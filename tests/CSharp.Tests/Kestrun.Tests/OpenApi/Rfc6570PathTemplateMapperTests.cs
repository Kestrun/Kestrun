using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

/// <summary>
/// Tests for RFC6570 path template mapping to Kestrel route patterns.
/// </summary>
public sealed class Rfc6570PathTemplateMapperTests
{
    [Theory]
    [InlineData("/users/{id}", "/users/{id}", "/users/{id}")]
    [InlineData("/files/{+path}", "/files/{+path}", "/files/{**path}")]
    [InlineData("/files/{path*}", "/files/{path*}", "/files/{**path}")]
    public void TryMapToKestrelRoute_MapsPathSegments(
        string template,
        string expectedOpenApi,
        string expectedKestrel)
    {
        var result = Rfc6570PathTemplateMapper.TryMapToKestrelRoute(
            template,
            out var mapping,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(expectedOpenApi, mapping.OpenApiPattern);
        Assert.Equal(expectedKestrel, mapping.KestrelPattern);
        Assert.Empty(mapping.QueryParameters);
    }

    [Fact]
    public void TryMapToKestrelRoute_ExtractsQueryParameters()
    {
        var result = Rfc6570PathTemplateMapper.TryMapToKestrelRoute(
            "/files/{+path}{?id,filter}",
            out var mapping,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal("/files/{+path}", mapping.OpenApiPattern);
        Assert.Equal("/files/{**path}", mapping.KestrelPattern);
        Assert.Contains("id", mapping.QueryParameters);
        Assert.Contains("filter", mapping.QueryParameters);
    }

    [Theory]
    [InlineData("/files/{#frag}")]
    [InlineData("/files/{?}")]
    public void TryMapToKestrelRoute_RejectsUnsupportedExpressions(string template)
    {
        var result = Rfc6570PathTemplateMapper.TryMapToKestrelRoute(
            template,
            out var mapping,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Equal(string.Empty, mapping.OpenApiPattern);
        Assert.Equal(string.Empty, mapping.KestrelPattern);
    }
}
