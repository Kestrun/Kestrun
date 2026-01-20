using System.Globalization;
using System.Collections.Specialized;
using System.Numerics;
using Kestrun.Utilities.Yaml;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace KestrunTests.YamlUtilities;

public class YamlTypeConverterTests
{
    [Fact]
    public void ConvertValueToProperType_NonScalar_Passthrough()
    {
        var mapping = new YamlMappingNode();
        var converted = YamlTypeConverter.ConvertValueToProperType(mapping);
        Assert.Same(mapping, converted);
    }

    [Fact]
    public void ConvertValueToProperType_TaggedNull_ReturnsNull()
    {
        var scalar = new YamlScalarNode("anything")
        {
            Tag = new TagName("tag:yaml.org,2002:null"),
            Style = ScalarStyle.Plain,
        };

        Assert.Null(YamlTypeConverter.ConvertValueToProperType(scalar));
    }

    [Fact]
    public void ConvertValueToProperType_TaggedBool_Parses_And_Rejects_Invalid()
    {
        var scalarTrue = new YamlScalarNode("true")
        {
            Tag = new TagName("tag:yaml.org,2002:bool"),
            Style = ScalarStyle.Plain,
        };
        var convertedTrue = YamlTypeConverter.ConvertValueToProperType(scalarTrue);
        _ = Assert.IsType<bool>(convertedTrue);
        Assert.True((bool)convertedTrue);

        var scalarBad = new YamlScalarNode("notabool")
        {
            Tag = new TagName("tag:yaml.org,2002:bool"),
            Style = ScalarStyle.Plain,
        };
        _ = Assert.Throws<FormatException>(() => YamlTypeConverter.ConvertValueToProperType(scalarBad));
    }

    [Theory]
    [InlineData("0x10", 16)]
    [InlineData("0o10", 8)]
    public void ConvertValueToProperType_TaggedInt_BasePrefixed(string input, int expected)
    {
        var scalar = new YamlScalarNode(input)
        {
            Tag = new TagName("tag:yaml.org,2002:int"),
            Style = ScalarStyle.Plain,
        };

        var converted = YamlTypeConverter.ConvertValueToProperType(scalar);
        Assert.Equal(expected, converted);
    }

    [Theory]
    [InlineData("1e3", 1000)]
    [InlineData("-1e+3", -1000)]
    public void ConvertValueToProperType_TaggedInt_Scientific(string input, int expected)
    {
        var scalar = new YamlScalarNode(input)
        {
            Tag = new TagName("tag:yaml.org,2002:int"),
            Style = ScalarStyle.Plain,
        };

        var converted = YamlTypeConverter.ConvertValueToProperType(scalar);
        Assert.Equal(expected, converted);
    }

    [Fact]
    public void ConvertValueToProperType_TaggedInt_Scientific_NegativeExponent_Throws()
    {
        var scalar = new YamlScalarNode("1e-3")
        {
            Tag = new TagName("tag:yaml.org,2002:int"),
            Style = ScalarStyle.Plain,
        };

        _ = Assert.Throws<FormatException>(() => YamlTypeConverter.ConvertValueToProperType(scalar));
    }

    [Fact]
    public void ConvertValueToProperType_TaggedInt_Downcasts_And_Promotes()
    {
        var intScalar = new YamlScalarNode("42")
        {
            Tag = new TagName("tag:yaml.org,2002:int"),
            Style = ScalarStyle.Plain,
        };
        _ = Assert.IsType<int>(YamlTypeConverter.ConvertValueToProperType(intScalar));

        var longScalar = new YamlScalarNode("2147483648") // int.MaxValue + 1
        {
            Tag = new TagName("tag:yaml.org,2002:int"),
            Style = ScalarStyle.Plain,
        };
        _ = Assert.IsType<long>(YamlTypeConverter.ConvertValueToProperType(longScalar));

        var bigScalar = new YamlScalarNode("123456789012345678901234567890")
        {
            Tag = new TagName("tag:yaml.org,2002:int"),
            Style = ScalarStyle.Plain,
        };
        _ = Assert.IsType<BigInteger>(YamlTypeConverter.ConvertValueToProperType(bigScalar));
    }

    [Theory]
    [InlineData(".inf")]
    [InlineData("inf")]
    [InlineData("infinity")]
    [InlineData("+.inf")]
    public void ConvertValueToProperType_TaggedFloat_Infinity_Positive(string input)
    {
        var scalar = new YamlScalarNode(input)
        {
            Tag = new TagName("tag:yaml.org,2002:float"),
            Style = ScalarStyle.Plain,
        };

        var converted = YamlTypeConverter.ConvertValueToProperType(scalar);
        _ = Assert.IsType<double>(converted);
        Assert.Equal(double.PositiveInfinity, (double)converted);
    }

    [Theory]
    [InlineData("-.inf")]
    [InlineData("-inf")]
    [InlineData("-infinity")]
    public void ConvertValueToProperType_TaggedFloat_Infinity_Negative(string input)
    {
        var scalar = new YamlScalarNode(input)
        {
            Tag = new TagName("tag:yaml.org,2002:float"),
            Style = ScalarStyle.Plain,
        };

        var converted = YamlTypeConverter.ConvertValueToProperType(scalar);
        _ = Assert.IsType<double>(converted);
        Assert.Equal(double.NegativeInfinity, (double)converted);
    }

    [Fact]
    public void ConvertValueToProperType_TaggedFloat_ParsesDecimal()
    {
        var scalar = new YamlScalarNode("1.25")
        {
            Tag = new TagName("tag:yaml.org,2002:float"),
            Style = ScalarStyle.Plain,
        };

        var converted = YamlTypeConverter.ConvertValueToProperType(scalar);
        _ = Assert.IsType<decimal>(converted);
        Assert.Equal(1.25m, (decimal)converted);
    }

    [Fact]
    public void ConvertValueToProperType_TaggedTimestamp_Uses_Local_For_ZoneAware_And_Unspecified_For_Naive()
    {
        var zoned = new YamlScalarNode("2020-01-01T00:00:00Z")
        {
            Tag = new TagName("tag:yaml.org,2002:timestamp"),
            Style = ScalarStyle.Plain,
        };
        var convertedZoned = YamlTypeConverter.ConvertValueToProperType(zoned);
        var dtZoned = Assert.IsType<DateTime>(convertedZoned);
        Assert.Equal(DateTimeKind.Local, dtZoned.Kind);

        var naive = new YamlScalarNode("2020-01-01")
        {
            Tag = new TagName("tag:yaml.org,2002:timestamp"),
            Style = ScalarStyle.Plain,
        };
        var convertedNaive = YamlTypeConverter.ConvertValueToProperType(naive);
        var dtNaive = Assert.IsType<DateTime>(convertedNaive);
        Assert.Equal(DateTimeKind.Unspecified, dtNaive.Kind);
    }

    [Fact]
    public void ConvertValueToProperType_PlainStyle_Heuristics_And_NullTokens()
    {
        var b = new YamlScalarNode("false") { Style = ScalarStyle.Plain };
        Assert.Equal(false, YamlTypeConverter.ConvertValueToProperType(b));

        var i = new YamlScalarNode("123") { Style = ScalarStyle.Plain };
        Assert.Equal(123, YamlTypeConverter.ConvertValueToProperType(i));

        var sci = new YamlScalarNode("2e3") { Style = ScalarStyle.Plain };
        Assert.Equal(2000, YamlTypeConverter.ConvertValueToProperType(sci));

        var dec = new YamlScalarNode("1.5") { Style = ScalarStyle.Plain };
        Assert.Equal(1.5m, YamlTypeConverter.ConvertValueToProperType(dec));

        var explicitNull = new YamlScalarNode("~") { Style = ScalarStyle.Plain };
        Assert.Null(YamlTypeConverter.ConvertValueToProperType(explicitNull));

        var explicitNullEmpty = new YamlScalarNode(string.Empty) { Style = ScalarStyle.Plain };
        Assert.Null(YamlTypeConverter.ConvertValueToProperType(explicitNullEmpty));
    }

    [Fact]
    public void ConvertValueToProperType_NonPlainStyle_DoesNotApply_Heuristics()
    {
        var quoted = new YamlScalarNode("true") { Style = ScalarStyle.DoubleQuoted };
        var converted = YamlTypeConverter.ConvertValueToProperType(quoted);
        _ = Assert.IsType<string>(converted);
        Assert.Equal("true", converted);
    }

    [Fact]
    public void ConvertYamlDocumentToPSObject_Mapping_Ordered_And_SpecialDatesAsStrings()
    {
        var map = new YamlMappingNode
        {
            { "b", "2" },
            { "a", "1" },
        };

        var ordered = YamlTypeConverter.ConvertYamlDocumentToPSObject(map, ordered: true);
        var od = Assert.IsType<OrderedDictionary>(ordered);

        var keys = od.Keys.Cast<object>().Select(k => k.ToString()).ToArray();
        Assert.Equal(new[] { "b", "a" }, keys);

        var datesMap = new YamlMappingNode
        {
            {
                "datesAsStrings",
                new YamlSequenceNode(new YamlScalarNode("2020-01-01"), new YamlScalarNode("2020-01-02"))
            },
        };

        var convertedDates = YamlTypeConverter.ConvertYamlDocumentToPSObject(datesMap, ordered: false);
        var dict = Assert.IsType<Dictionary<string, object?>>(convertedDates);
        var arr = Assert.IsType<string[]>(dict["datesAsStrings"]);
        Assert.Equal(new[] { "2020-01-01", "2020-01-02" }, arr);
    }

    [Fact]
    public void ConvertYamlDocumentToPSObject_KeyValuePair_Overload_Unwraps_Value()
    {
        var map = new YamlMappingNode { { "a", "1" } };
        var child = map.Children.Single();

        var converted = YamlTypeConverter.ConvertYamlDocumentToPSObject(child, ordered: false);
        Assert.NotNull(converted);
        Assert.Equal(1L, Convert.ToInt64(converted, CultureInfo.InvariantCulture));
    }
}
