using Kestrun.Utilities.Yaml;
using Xunit;
using System.Numerics;

namespace KestrunTests;

public class YamlIntParsingTests
{
    private static object InvokeParse(string scalar, string tag = "tag:yaml.org,2002:int")
    {
        var node = new YamlDotNet.RepresentationModel.YamlScalarNode(scalar)
        {
            Tag = new YamlDotNet.Core.TagName(tag)
        };
        return YamlTypeConverter.ConvertValueToProperType(node)!;
    }

    [Fact]
    public void Scientific_PositiveExponent_Downcasts()
    {
        var v = InvokeParse("12e2"); // 12 * 10^2 = 1200
        Assert.IsType<int>(v);
        Assert.Equal(1200, v);
    }

    [Fact]
    public void Scientific_BigInteger_Result()
    {
        var v = InvokeParse("12345678901234567890e3");
        Assert.IsType<BigInteger>(v);
    }

    [Fact]
    public void Octal_Prefix_Parses()
    {
        var v = InvokeParse("0o10"); // 8
        Assert.Equal(8, v);
    }

    [Fact]
    public void Hex_Prefix_Parses()
    {
        var v = InvokeParse("0xFF");
        Assert.Equal(255, v);
    }

    [Fact]
    public void Generic_BigInteger_Downcasts_Long()
    {
        var bigLongStr = ((long)int.MaxValue + 10L).ToString();
        var v = InvokeParse(bigLongStr);
        Assert.IsType<long>(v);
    }

    [Fact]
    public void Generic_VeryLarge_RemainsBigInteger()
    {
        var huge = new string('9', 60); // large number
        var v = InvokeParse(huge);
        Assert.IsType<BigInteger>(v);
    }
}
