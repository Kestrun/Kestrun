using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public class OaSchemaTypeExtensionsTests
{
    [Theory]
    [InlineData(OaSchemaType.String, JsonSchemaType.String)]
    [InlineData(OaSchemaType.Number, JsonSchemaType.Number)]
    [InlineData(OaSchemaType.Integer, JsonSchemaType.Integer)]
    [InlineData(OaSchemaType.Boolean, JsonSchemaType.Boolean)]
    [InlineData(OaSchemaType.Array, JsonSchemaType.Array)]
    [InlineData(OaSchemaType.Object, JsonSchemaType.Object)]
    [InlineData(OaSchemaType.Null, JsonSchemaType.Null)]
    public void ToJsonSchemaType_KnownValues_ReturnsExpected(OaSchemaType input, JsonSchemaType expected)
    {
        var result = input.ToJsonSchemaType();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToJsonSchemaType_None_ReturnsNull()
    {
        var result = OaSchemaType.None.ToJsonSchemaType();

        Assert.Null(result);
    }

    [Fact]
    public void ToJsonSchemaType_UnknownEnumValue_ReturnsNull()
    {
        var result = ((OaSchemaType)999).ToJsonSchemaType();

        Assert.Null(result);
    }
}
