using Kestrun.Utilities.Yaml;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace KestrunTests.YamlUtilities;

public class YamlHelperAndLoaderTests
{
    [Fact]
    public void YamlHelper_Serializes_Anonymous_Type()
    {
        var yaml = YamlHelper.ToYaml(new { a = 1, b = "x" });
        Assert.Contains("a: 1", yaml);
        Assert.Contains("b: x", yaml);
    }

    [Fact]
    public void YamlHelper_Leaves_Blank_For_Null_Dict_Value()
    {
        var dict = new Dictionary<string, object?> { ["a"] = null };
        var yaml = YamlHelper.ToYaml(dict);
        Assert.Contains("a:", yaml.Trim());
    }

    [Fact]
    public void YamlLoader_Gets_Root_Nodes_Single_Doc()
    {
        var nodes = YamlLoader.GetRootNodes("a: 1\n");
        Assert.Single(nodes);
        Assert.IsType<YamlMappingNode>(nodes[0]);
    }

    [Fact]
    public void YamlLoader_Multiple_Documents()
    {
        var text = "---\na: 1\n---\n- 1\n- 2\n";
        var nodes = YamlLoader.GetRootNodes(text);
        Assert.Equal(2, nodes.Count);
        Assert.IsType<YamlMappingNode>(nodes[0]);
        Assert.IsType<YamlSequenceNode>(nodes[1]);
    }

    [Fact]
    public void YamlLoader_MergingParser_Works_For_Aliases()
    {
        var text = "defaults: &def\n  a: 1\n  b: 2\nobj:\n  <<: *def\n  c: 3\n";
        var objs = YamlLoader.DeserializeToObjects(text, useMergingParser: true);
        var root = Assert.IsType<Dictionary<string, object?>>(objs[0]);
        var obj = Assert.IsType<Dictionary<string, object?>>(root["obj"]);
        Assert.Equal(1, obj["a"]);
        Assert.Equal(2, obj["b"]);
        Assert.Equal(3, obj["c"]);
    }
}
