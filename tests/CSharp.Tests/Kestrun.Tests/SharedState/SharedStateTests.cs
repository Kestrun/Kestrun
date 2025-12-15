using Kestrun.Hosting;
using Xunit;

namespace KestrunTests.SharedState;

public class SharedStateTests
{
    // ── constructor tests ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Constructor_Default_CreatesOrdinalIgnoreCaseStore()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.True(state.Set("Key", "value"));
        Assert.True(state.Contains("key")); // Case-insensitive
        Assert.True(state.Contains("KEY"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Constructor_WithOrdinalComparison_CreatesCaseSensitiveStore()
    {
        var state = new Kestrun.SharedState.SharedState(ordinalIgnoreCase: false);
        Assert.True(state.Set("Key", "value"));
        Assert.True(state.Contains("Key"));
        Assert.False(state.Contains("key")); // Case-sensitive
    }

    // ── Set method tests ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_And_TryGet_Work()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);

        Assert.True(host.SharedState.Set("foo", new List<int> { 1, 2 }));
        Assert.True(host.SharedState.TryGet("foo", out List<int>? list));
        Assert.Equal(2, list?.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_WithNullValue_Succeeds()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.True(state.Set("nullVal", null));
        Assert.True(state.Contains("nullVal"));
        Assert.Null(state.Get("nullVal"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_Overwrites_ExistingValue()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.True(state.Set("key", "original"));
        Assert.True(state.Set("key", "updated"));
        Assert.Equal("updated", state.Get("key"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_WithValueType_ThrowsWhenNotAllowed()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        _ = Assert.Throws<ArgumentException>(() => host.SharedState.Set("num", 123));
        _ = Assert.Throws<ArgumentException>(() => host.SharedState.Set("flag", true));
        _ = Assert.Throws<ArgumentException>(() => host.SharedState.Set("date", DateTime.Now));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_WithValueType_SucceedsWhenAllowed()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.True(state.Set("num", 123, allowsValueType: true));
        Assert.True(state.Set("flag", true, allowsValueType: true));
        Assert.True(state.Set("date", DateTime.Now, allowsValueType: true));
        Assert.Equal(3, state.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1bad")]
    [InlineData("bad-name")]
    [InlineData("bad name")]
    [InlineData("bad@name")]
    [Trait("Category", "SharedState")]
    public void Set_WithInvalidName_ThrowsArgumentException(string name)
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = Assert.Throws<ArgumentException>(() => state.Set(name, "value"));
    }

    // ── Contains method tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Contains_ExistingKey_ReturnsTrue()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("exists", "value");
        Assert.True(state.Contains("exists"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Contains_MissingKey_ReturnsFalse()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.False(state.Contains("missing"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Contains_CaseInsensitive_ReturnsTrue()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("KeyName", "value");
        Assert.True(state.Contains("keyname"));
        Assert.True(state.Contains("KEYNAME"));
    }

    // ── TryGet method tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_ExistingKey_ReturnsTrue()
    {
        var state = new Kestrun.SharedState.SharedState();
        var list = new List<string> { "a", "b" };
        _ = state.Set("list", list);
        Assert.True(state.TryGet<List<string>>("list", out var result));
        Assert.Same(list, result);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.False(state.TryGet<string>("missing", out var result));
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_WrongType_ReturnsFalse()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("str", "text");
        Assert.False(state.TryGet<int>("str", out var result));
        Assert.Equal(0, result);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_NullValue_ReturnsFalse()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("nullVal", null);
        // TryGet returns false for null values because null is not an instance of any type
        Assert.False(state.TryGet<string?>("nullVal", out var result));
        Assert.Null(result);
    }

    // ── Get method tests ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Get_ExistingKey_ReturnsValue()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("key", "value");
        Assert.Equal("value", state.Get("key"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Get_MissingKey_ReturnsNull()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.Null(state.Get("missing"));
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

    // ── Snapshot method tests ────────────────────────────────────────
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

    [Fact]
    [Trait("Category", "SharedState")]
    public void Snapshot_ReturnsShallowCopy()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("key1", "val1");
        _ = state.Set("key2", "val2");

        var snapshot = state.Snapshot();
        Assert.Equal(2, snapshot.Count);

        // Add new key after snapshot
        _ = state.Set("key3", "val3");

        // Snapshot should still be 2 items
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, state.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void KeySnapshot_ReturnsAllKeys()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("alpha", "a");
        _ = state.Set("beta", "b");
        _ = state.Set("gamma", "c");

        var keys = state.KeySnapshot();
        Assert.Equal(3, keys.Count);
        Assert.Contains("alpha", keys);
        Assert.Contains("beta", keys);
        Assert.Contains("gamma", keys);
    }

    // ── Clear method tests ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Clear_RemovesAllEntries()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("key1", "val1");
        _ = state.Set("key2", "val2");
        _ = state.Set("key3", "val3");
        Assert.Equal(3, state.Count);

        state.Clear();
        Assert.Equal(0, state.Count);
        Assert.False(state.Contains("key1"));
    }

    // ── Count property tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Count_ReflectsCurrentSize()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.Equal(0, state.Count);

        _ = state.Set("key1", "val1");
        Assert.Equal(1, state.Count);

        _ = state.Set("key2", "val2");
        Assert.Equal(2, state.Count);

        _ = state.Remove("key1");
        Assert.Equal(1, state.Count);

        state.Clear();
        Assert.Equal(0, state.Count);
    }

    // ── Keys property tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Keys_ReturnsAllKeys()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("first", "1");
        _ = state.Set("second", "2");

        var keys = state.Keys.ToList();
        Assert.Equal(2, keys.Count);
        Assert.Contains("first", keys);
        Assert.Contains("second", keys);
    }

    // ── Values property tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Values_ReturnsAllValues()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("key1", "val1");
        _ = state.Set("key2", "val2");

        var values = state.Values.ToList();
        Assert.Equal(2, values.Count);
        Assert.Contains("val1", values);
        Assert.Contains("val2", values);
    }

    // ── Remove method tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Remove_ExistingKey_ReturnsTrueAndRemoves()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("key", "value");
        Assert.True(state.Contains("key"));

        Assert.True(state.Remove("key"));
        Assert.False(state.Contains("key"));
        Assert.Equal(0, state.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Remove_MissingKey_ReturnsFalse()
    {
        var state = new Kestrun.SharedState.SharedState();
        Assert.False(state.Remove("missing"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Remove_CaseInsensitive_Works()
    {
        var state = new Kestrun.SharedState.SharedState();
        _ = state.Set("KeyName", "value");
        Assert.True(state.Remove("keyname"));
        Assert.False(state.Contains("KeyName"));
    }

    // ── concurrent access tests ────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public async Task ConcurrentSet_MaintainsThreadSafety()
    {
        var state = new Kestrun.SharedState.SharedState();
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => state.Set($"key{index}", $"val{index}")));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(100, state.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public async Task ConcurrentRemove_MaintainsThreadSafety()
    {
        var state = new Kestrun.SharedState.SharedState();

        // Populate
        for (var i = 0; i < 100; i++)
        {
            _ = state.Set($"key{i}", $"val{i}");
        }

        var tasks = new List<Task>();
        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => state.Remove($"key{index}")));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(0, state.Count);
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
