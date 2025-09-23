using Kestrun.Utilities.Yaml;
using Xunit;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core;
using System.Numerics;

namespace KestrunTests;

public class YamlScalarConversionTests
{
    private static object? Convert(string text, string? tag = null, ScalarStyle style = ScalarStyle.Plain)
    {
        var node = new YamlScalarNode(text) { Style = style };
        if (tag != null)
        {
            node.Tag = new YamlDotNet.Core.TagName(tag);
        }
        return YamlTypeConverter.ConvertValueToProperType(node);
    }

    [Fact]
    public void Tagged_String_ReturnsString()
    {
        var v = Convert("hello", "tag:yaml.org,2002:str");
        Assert.Equal("hello", v);
    }

    [Fact]
    public void Tagged_Null_ReturnsNull()
    {
        var v = Convert("ignored", "tag:yaml.org,2002:null");
        Assert.Null(v);
    }

    [Fact]
    public void Tagged_Bool_Parses()
    {
        var v = Convert("true", "tag:yaml.org,2002:bool");
        Assert.Equal(true, v);
    }

    [Fact]
    public void Tagged_Int_Parses()
    {
        var v = Convert("123", "tag:yaml.org,2002:int");
        Assert.Equal(123, v);
    }

    [Fact]
    public void Tagged_Float_Infinity()
    {
        var v = Convert(".inf", "tag:yaml.org,2002:float");
        Assert.Equal(double.PositiveInfinity, v);
    }

    [Fact]
    public void Tagged_Float_Decimal()
    {
        var v = Convert("1.25", "tag:yaml.org,2002:float");
        Assert.IsType<decimal>(v);
        Assert.Equal(1.25m, v);
    }

    [Fact]
    public void Tagged_Timestamp_Zoned()
    {
        var iso = DateTimeOffset.UtcNow.ToString("o");
        var v = Convert(iso, "tag:yaml.org,2002:timestamp");
        Assert.IsType<DateTime>(v);
    }

    [Fact]
    public void Plain_Bool()
    {
        var v = Convert("false");
        Assert.Equal(false, v);
    }

    [Fact]
    public void Plain_Int_Downcast()
    {
        var v = Convert("42");
        Assert.Equal(42, v);
    }

    [Fact]
    public void Plain_BigInteger()
    {
        var big = new string('9', 50);
        var v = Convert(big);
        Assert.IsType<BigInteger>(v);
    }

    [Fact]
    public void Plain_Decimal()
    {
        var v = Convert("3.14");
        Assert.IsType<decimal>(v);
    }

    [Fact]
    public void Plain_DoubleOrDecimal_Fallback()
    {
        var v = Convert("1e-2"); // negative exponent -> not integer; decimal.TryParse succeeds first
        Assert.True(v is decimal or double, $"Expected decimal or double, got {v?.GetType().FullName}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("~")]
    [InlineData("$null")]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    public void Plain_NullTokens(string token)
    {
        var v = Convert(token);
        Assert.Null(v);
    }

    [Fact]
    public void Plain_FallbackString()
    {
        var v = Convert("not_a_number_xyz");
        Assert.Equal("not_a_number_xyz", v);
    }
}
