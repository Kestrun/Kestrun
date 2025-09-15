using System.Collections;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class ReadOnlyDictionaryAdapterTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void Indexer_ReturnsValueOrNull()
    {
        IDictionary inner = new Hashtable
        {
            ["a"] = 1,
            ["b"] = null
        };
        var ro = new ReadOnlyDictionaryAdapter(inner);

        Assert.Equal(1, ro["a"]);
        Assert.Null(ro["b"]);
        Assert.Null(ro["missing"]);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Keys_And_Values_Enumerate()
    {
        IDictionary inner = new Hashtable
        {
            ["x"] = 42,
            ["y"] = "z"
        };
        var ro = new ReadOnlyDictionaryAdapter(inner);

        Assert.Contains("x", ro.Keys);
        Assert.Contains("y", ro.Keys);
        Assert.Contains(42, ro.Values);
        Assert.Contains("z", ro.Values);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Contains_TryGet_And_Count_Work()
    {
        IDictionary inner = new Hashtable
        {
            ["k"] = "v"
        };
        var ro = new ReadOnlyDictionaryAdapter(inner);

        Assert.True(ro.ContainsKey("k"));
        Assert.False(ro.ContainsKey(null!));
        Assert.True(ro.TryGetValue("k", out var v));
        Assert.Equal("v", v);
        Assert.False(ro.TryGetValue("missing", out _));
        _ = Assert.Single(ro);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void GetEnumerator_YieldsStringKeys()
    {
        IDictionary inner = new Hashtable
        {
            [1] = "num",
            ["s"] = 2
        };
        var ro = new ReadOnlyDictionaryAdapter(inner);

        var pairs = ro.ToList();
        Assert.Contains(pairs, kv => kv.Key == "1" && (string)kv.Value! == "num");
        Assert.Contains(pairs, kv => kv.Key == "s" && (int)kv.Value! == 2);
    }
}
