using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

public class MediaTypeHelperTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("application/json", "application/json")]
    [InlineData("Application/JSON", "application/json")]
    [InlineData("application/json; charset=utf-8", "application/json")]
    [InlineData("text/xml; charset=utf-8", "text/xml")]
    public void Normalize_StripsParameters_AndLowercases(string? input, string expected)
        => Assert.Equal(expected, MediaTypeHelper.Normalize(input));

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("application/json", "application/json")]
    [InlineData("application/json; charset=utf-8", "application/json")]
    [InlineData("application/x-yaml", "application/yaml")]
    [InlineData("text/yaml", "application/yaml")]
    [InlineData("text/xml", "application/xml")]
    [InlineData("application/bson", "application/bson")]
    [InlineData("application/cbor", "application/cbor")]
    [InlineData("text/csv", "text/csv")]
    [InlineData("application/x-www-form-urlencoded", "application/x-www-form-urlencoded")]
    [InlineData("*/*", "text/plain")]
    [InlineData("text/*", "text/plain")]
    [InlineData("application/*", "text/plain")]
    public void Canonicalize_MapsKnownTypes(string? input, string expected)
        => Assert.Equal(expected, MediaTypeHelper.Canonicalize(input));

    [Theory]
    [InlineData("application/vnd.foo+json", "application/json")]
    [InlineData("application/vnd.foo+xml", "application/xml")]
    [InlineData("application/vnd.foo+yaml", "application/yaml")]
    [InlineData("application/vnd.foo+yml", "application/yaml")]
    [InlineData("application/vnd.foo+cbor", "application/cbor")]
    [InlineData("application/vnd.foo+json; charset=utf-8", "application/json")]
    public void Canonicalize_MapsStructuredSuffixes(string input, string expected)
        => Assert.Equal(expected, MediaTypeHelper.Canonicalize(input));

    [Fact]
    public void Canonicalize_UnknownType_ReturnsNormalized()
        => Assert.Equal("image/png", MediaTypeHelper.Canonicalize("IMAGE/PNG; q=0.9"));
}
