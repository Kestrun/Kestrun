using Kestrun.Hosting;
using Kestrun.SharedState;
using Xunit;

namespace KestrunTests.SharedState;

public class SharedStateTests
{
    // ── happy‑path basics ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_And_TryGet_Work()
    {
        _ = new KestrunHost("TestHost", AppContext.BaseDirectory);

        Assert.True(SharedStateStore.Set("foo", new List<int> { 1, 2 }));
        Assert.True(SharedStateStore.TryGet("foo", out List<int>? list));
        Assert.Equal(2, list?.Count);
    }

    // ── case sensitivity ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void CaseInsensitive_Access_Works()
    {
        _ = new KestrunHost("TestHost", AppContext.BaseDirectory);
        _ = SharedStateStore.Set("Bar", "baz");

        Assert.True(SharedStateStore.TryGet("bar", out string? val));
        Assert.Equal("baz", val);
    }



    // ── snapshot helpers ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Snapshot_And_KeySnapshot_Work()
    {
        _ = new KestrunHost("TestHost", AppContext.BaseDirectory);
        _ = SharedStateStore.Set("snap", "val");

        var map = SharedStateStore.Snapshot();
        var keys = SharedStateStore.KeySnapshot();

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
        _ = Assert.Throws<ArgumentException>(() => SharedStateStore.Set("1bad", "oops"));
        _ = Assert.Throws<ArgumentException>(() => SharedStateStore.Set("bad-name", "oops"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void ValueType_Throws()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        _ = Assert.Throws<ArgumentException>(() => SharedStateStore.Set("num", 123)); // int ⇒ value‑type
    }
}
