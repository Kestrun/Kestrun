using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public class OaParameterExtensionsTests
{
    [Theory]
    [InlineData(OaParameterLocation.Query, ParameterLocation.Query)]
    [InlineData(OaParameterLocation.Header, ParameterLocation.Header)]
    [InlineData(OaParameterLocation.Path, ParameterLocation.Path)]
    [InlineData(OaParameterLocation.Cookie, ParameterLocation.Cookie)]
    public void ToOpenApi_WithLocation_MapsExpected(OaParameterLocation input, ParameterLocation expected)
    {
        Assert.Equal(expected, input.ToOpenApi());
        Assert.Equal(expected, input.ToParameterLocation());
    }

    [Fact]
    public void ToOpenApi_WithInvalidLocation_ThrowsArgumentOutOfRangeException()
    {
        var input = (OaParameterLocation)999;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => input.ToOpenApi());
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => input.ToParameterLocation());
    }

    [Theory]
    [InlineData(OaParameterStyle.Simple, ParameterStyle.Simple)]
    [InlineData(OaParameterStyle.Form, ParameterStyle.Form)]
    [InlineData(OaParameterStyle.Matrix, ParameterStyle.Matrix)]
    [InlineData(OaParameterStyle.Label, ParameterStyle.Label)]
    [InlineData(OaParameterStyle.SpaceDelimited, ParameterStyle.SpaceDelimited)]
    [InlineData(OaParameterStyle.PipeDelimited, ParameterStyle.PipeDelimited)]
    [InlineData(OaParameterStyle.DeepObject, ParameterStyle.DeepObject)]
    public void ToOpenApi_WithStyle_MapsExpected(OaParameterStyle input, ParameterStyle expected)
    {
        Assert.Equal(expected, input.ToOpenApi());
        Assert.Equal(expected, ((OaParameterStyle?)input).ToParameterStyle());
    }

    [Fact]
    public void ToOpenApi_WithInvalidStyle_ThrowsArgumentOutOfRangeException()
    {
        var input = (OaParameterStyle)999;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => input.ToOpenApi());
    }

    [Fact]
    public void ToParameterStyle_WithNull_ThrowsArgumentOutOfRangeException()
    {
        OaParameterStyle? style = null;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => style.ToParameterStyle());
    }

    [Theory]
    [InlineData("query", ParameterLocation.Query)]
    [InlineData("HEADER", ParameterLocation.Header)]
    [InlineData("Path", ParameterLocation.Path)]
    [InlineData("CoOkIe", ParameterLocation.Cookie)]
    public void ToOpenApiParameterLocation_ValidStrings_MapsExpected(string input, ParameterLocation expected)
    {
        var result = input.ToOpenApiParameterLocation();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToOpenApiParameterLocation_UnknownString_ThrowsArgumentOutOfRangeException() => _ = Assert.Throws<ArgumentOutOfRangeException>(() => "body".ToOpenApiParameterLocation());
}
