using Kestrun.Hosting;
using Xunit;

namespace KestrunTests.SharedState;

public class SharedStateTests
{
    // ── happy‑path basics ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_And_TryGet_Work()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);

        Assert.True(host.SharedState.Set("foo", new List<int> { 1, 2 }));
        Assert.True(host.SharedState.TryGet("foo", out List<int>? list));
        Assert.Equal(2, list?.Count);
    }

    // ── case sensitivity ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void CaseInsensitive_Access_Works()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        Assert.True(host.SharedState.Set("Bar", "baz"));

        Assert.True(host.SharedState.TryGet("bar", out string? val));
        Assert.Equal("baz", val);
    }



    // ── snapshot helpers ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Snapshot_And_KeySnapshot_Work()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        Assert.True(host.SharedState.Set("snap", "val"));

        var map = host.SharedState.Snapshot();
        var keys = host.SharedState.KeySnapshot();

        Assert.True(map.ContainsKey("snap"));
        Assert.Equal("val", map["snap"]);
        Assert.Contains("snap", keys, StringComparer.OrdinalIgnoreCase);
    }

    // ── defensive guards ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Invalid_Name_Throws()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        _ = Assert.Throws<ArgumentException>(() => host.SharedState.Set("1bad", "oops"));
        _ = Assert.Throws<ArgumentException>(() => host.SharedState.Set("bad-name", "oops"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void ValueType_Throws()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        _ = Assert.Throws<ArgumentException>(() => host.SharedState.Set("num", 123)); // int ⇒ value‑type
    }
}
