using Kestrun.Utilities.Yaml;
using Xunit;

namespace KestrunTests.YamlUtilities;

public class YamlSerializerFactoryTests
{
    private static string Serialize(object? o, SerializationOptions opts)
    {
        var ser = YamlSerializerFactory.GetSerializer(opts);
        using var sw = new StringWriter();
        ser.Serialize(sw, o);
        return sw.ToString();
    }

    private sealed class Sample
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    [Fact]
    public void OmitNullValues_Removes_Null_Property()
    {
        var yaml = Serialize(new Sample { A = 5, B = null }, SerializationOptions.OmitNullValues | SerializationOptions.EmitDefaults);
        Assert.Contains("A: 5", yaml);
        Assert.DoesNotContain("B:", yaml);
    }

    [Fact]
    public void EmitDefaults_Includes_Null_Property()
    {
        var yaml = Serialize(new Sample { A = 5, B = null }, SerializationOptions.EmitDefaults);
        Assert.Contains("A: 5", yaml);
        Assert.Contains("B:", yaml); // blank null
    }

    [Fact]
    public void UseFlowStyle_Flattens_Output()
    {
        var yaml = Serialize(new { a = 1, b = 2 }, SerializationOptions.EmitDefaults | SerializationOptions.UseFlowStyle);
        var compact = yaml.Replace(" ", string.Empty).Replace("\r\n", "");
        Assert.Contains(/*lang=json*/ "{a:1,b:2}", compact);
    }

    [Fact]
    public void UseSequenceFlowStyle_Flattens_Sequences_Only()
    {
        var yaml = Serialize(new { list = new[] { 1, 2, 3 } }, SerializationOptions.EmitDefaults | SerializationOptions.UseSequenceFlowStyle);
        var flat = yaml.Replace("\r\n", " ");
        Assert.Contains("list: [1, 2, 3]", flat);
        Assert.DoesNotContain("{a:", flat); // mapping not forced to flow
    }

    [Fact]
    public void WithIndentedSequences_Applies_Indent()
    {
        var yaml = Serialize(new { list = new[] { 1, 2 } }, SerializationOptions.EmitDefaults | SerializationOptions.WithIndentedSequences);
        Assert.Contains("list:", yaml);
        // Expect each sequence item to be on its own line and indented (dash-space pattern)
        Assert.Matches(@"list:\r?\n\s*- 1\r?\n\s*- 2", yaml);
    }
}
