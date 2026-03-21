using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public class OpenApiSpecVersionExtensionsTests
{
    [Theory]
    [InlineData("2.0", OpenApiSpecVersion.OpenApi2_0)]
    [InlineData("v3.0", OpenApiSpecVersion.OpenApi3_0)]
    [InlineData("V3.0.4", OpenApiSpecVersion.OpenApi3_0)]
    [InlineData("3.1", OpenApiSpecVersion.OpenApi3_1)]
    [InlineData("3.1.1", OpenApiSpecVersion.OpenApi3_1)]
    [InlineData("3.2", OpenApiSpecVersion.OpenApi3_2)]
    [InlineData("3.2.0", OpenApiSpecVersion.OpenApi3_2)]
    public void ParseOpenApiSpecVersion_ValidInputs_ReturnsExpected(string input, OpenApiSpecVersion expected)
    {
        var result = input.ParseOpenApiSpecVersion();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("4.0")]
    [InlineData("v3.3")]
    [InlineData("")]
    public void ParseOpenApiSpecVersion_UnsupportedInput_ThrowsArgumentException(string input) => _ = Assert.Throws<ArgumentException>(() => input.ParseOpenApiSpecVersion());

    [Theory]
    [InlineData(OpenApiSpecVersion.OpenApi2_0, "2.0")]
    [InlineData(OpenApiSpecVersion.OpenApi3_0, "3.0.4")]
    [InlineData(OpenApiSpecVersion.OpenApi3_1, "3.1.2")]
    [InlineData(OpenApiSpecVersion.OpenApi3_2, "3.2.0")]
    public void ToVersionString_KnownValues_ReturnsExpected(OpenApiSpecVersion value, string expected)
    {
        var result = value.ToVersionString();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToVersionString_UnknownValue_ThrowsArgumentOutOfRangeException()
    {
        var unknown = (OpenApiSpecVersion)999;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => unknown.ToVersionString());
    }
}
