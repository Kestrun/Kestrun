using System.Collections;
using System.Numerics;
using System.Management.Automation;
using Kestrun.Utilities.Yaml;
using Xunit;

namespace KestrunTests.YamlUtilities;

public class YamlEmittersAndConvertersTests
{
    private static string Serialize(object? obj, SerializationOptions? opts = null) => YamlHelper.ToYaml(obj, opts);

    [Fact]
    public void BigIntegerTypeConverter_Writes_And_Reads()
    {
        var big = BigInteger.Parse("123456789012345678901234567890");
        var yaml = Serialize(big);
        Assert.Contains("123456789012345678901234567890", yaml);
    }

    [Fact]
    public void IDictionaryTypeConverter_Emits_Nulls_As_Blank_Unless_Omitted()
    {
        var dict = new Hashtable
        {
            ["a"] = 1,
            ["b"] = null,
            ["c"] = "",
        };
        var yamlKeepNulls = Serialize(dict, SerializationOptions.EmitDefaults);
        // b: (blank after colon) and empty string double quoted
        Assert.Contains("a: 1", yamlKeepNulls);
        Assert.Contains("b:", yamlKeepNulls);
        Assert.Contains("c: \"\"", yamlKeepNulls);

        var yamlOmitNull = Serialize(dict, SerializationOptions.OmitNullValues | SerializationOptions.EmitDefaults);
        Assert.DoesNotContain("b:", yamlOmitNull);
        Assert.Contains("c: \"\"", yamlOmitNull);
    }

    [Fact]
    public void IDictionaryTypeConverter_Unwraps_PSObject_BaseObject()
    {
        var dict = new Hashtable
        {
            ["x"] = PSObject.AsPSObject(5),
        };

        var yamlKeepNulls = Serialize(dict, SerializationOptions.EmitDefaults);
        Assert.Contains("x: 5", yamlKeepNulls);
    }

    [Fact]
    public void PSObjectTypeConverter_Serializes_Simple_And_FlowStyle()
    {
        var o = new PSObject();
        o.Properties.Add(new PSNoteProperty("Name", "alice"));
        o.Properties.Add(new PSNoteProperty("Age", 30));

        var yaml = Serialize(o, SerializationOptions.EmitDefaults);
        Assert.Contains("Name: alice", yaml);
        Assert.Contains("Age: 30", yaml);

        var flow = Serialize(o, SerializationOptions.EmitDefaults | SerializationOptions.UseFlowStyle);
        var flat = flow.Replace("\r\n", "");
        // Flow style output is fully compact (e.g., {Name:alice,Age:30})
        var compact = flat.Replace(" ", "");
        Assert.Contains("{Name:alice,Age:30}", compact);
    }

    [Fact]
    public void PSObjectTypeConverter_Serializes_BaseObject_When_Not_DictionaryLike()
    {
        var yaml = Serialize(PSObject.AsPSObject(123), SerializationOptions.EmitDefaults);
        Assert.Contains("123", yaml);
    }

    [Fact]
    public void PSObjectTypeConverter_Unwraps_Nested_PSObject_BaseObject_In_Properties()
    {
        var o = new PSObject();
        o.Properties.Add(new PSNoteProperty("n", PSObject.AsPSObject(7)));
        o.Properties.Add(new PSNoteProperty("empty", string.Empty));

        var yaml = Serialize(o, SerializationOptions.EmitDefaults);
        Assert.Contains("\"n\": 7", yaml);
        Assert.Contains("empty: \"\"", yaml);
    }

    [Fact]
    public void FlowStyleSequenceEmitter_Forces_Sequence_Flow()
    {
        var list = new[] { 1, 2, 3 };
        var yaml = Serialize(list, SerializationOptions.EmitDefaults | SerializationOptions.UseSequenceFlowStyle);
        Assert.Contains("[1, 2, 3]", yaml.Replace("\r\n", " "));
    }

    [Fact]
    public void FlowStyleAllEmitter_Forces_All_Flow()
    {
        var obj = new { a = new[] { 1, 2 }, b = 5 };
        var yaml = Serialize(obj, SerializationOptions.EmitDefaults | SerializationOptions.UseFlowStyle);
        var flat = yaml.Replace("\r\n", "");
        var compact = flat.Replace(" ", "");
        Assert.Contains(/*lang=json*/ "{a:[1,2],b:5}", compact);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("no")]
    [InlineData("01")]
    public void StringQuotingEmitter_Quotes_Ambiguous_Strings(string input)
    {
        var yaml = Serialize(new { x = input }, SerializationOptions.EmitDefaults);
        // Expect quotes if ambiguous token not empty null; 'null' w/ tag would be blank but here it's string property
        if (input == "null")
        {
            Assert.Contains("x: \"null\"", yaml);
        }
        else if (input == "01")
        {
            // Leading zero numeric -> should be quoted
            Assert.Contains("x: \"01\"", yaml);
        }
        else
        {
            Assert.Contains($"x: \"{input}\"", yaml);
        }
    }

    [Fact]
    public void StringQuotingEmitter_Uses_Literal_For_Newlines()
    {
        var yaml = Serialize(new { x = "a\nb" }, SerializationOptions.EmitDefaults);
        var flat = yaml.Replace("\r\n", "\n");
        Assert.Contains("x: |", flat);
        Assert.Contains("\n  a\n  b", flat);
    }
}
