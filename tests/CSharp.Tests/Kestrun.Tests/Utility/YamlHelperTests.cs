using Kestrun.Utilities;
using System.Collections;
using Xunit;

namespace KestrunTests.Utility;

public class YamlHelperTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void ToYaml_SerializesObject()
    {
        var ht = new Hashtable { { "name", "foo" }, { "value", 1 } };
        var yaml = YamlHelper.ToYaml(ht);
        Assert.Contains("name: foo", yaml);
        Assert.Contains("value: 1", yaml);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void FromYamlToHashtable_RoundTrip()
    {
        var yaml = "name: foo\nvalue: 1";
        var ht = YamlHelper.ToHashtable(yaml);
        Assert.Equal("foo", ht["name"]);
        Assert.NotNull(ht["value"]);
        Assert.Equal(1, Convert.ToInt32(ht["value"]));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void FromYamlToPSCustomObject_RoundTrip()
    {
        var yaml = "name: foo\nvalue: 1";
        var obj = YamlHelper.ToPSCustomObject(yaml);
        Assert.Equal("foo", obj.Members["name"].Value);
        Assert.Equal("1", obj.Members["value"].Value);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void ToYaml_SerializesHashtableAndList()
    {
        var ht = new Hashtable
        {
            ["a"] = 1,
            ["b"] = new ArrayList { "x", 2 }
        };
        var yaml = YamlHelper.ToYaml(ht);
        Assert.Contains("a:", yaml);
        Assert.Contains("b:", yaml);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void FromYaml_Deserializes_ToHashtable_And_PSCustomObject()
    {
        var yaml = "a: 1\nb:\n - x\n - 2\n";
        var ht = YamlHelper.ToHashtable(yaml);
        Assert.Equal(1, Convert.ToInt32(ht["a"]));

        var obj = YamlHelper.ToPSCustomObject(yaml);
        var aval = obj.Properties["a"].Value;
        Assert.Equal(1, Convert.ToInt32(aval));
    }
}
