using Kestrun.SharedState;
using Xunit;

namespace KestrunTests.SharedState;

[Collection("SharedStateSerial")] // Prevent parallel execution due to global state
public class GlobalStoreTests : IDisposable
{
    public GlobalStoreTests() =>
        // Clear before each test to ensure clean state
        GlobalStore.Clear();

    public void Dispose()
    {
        // Clear after each test
        GlobalStore.Clear();
        GC.SuppressFinalize(this);
    }

    // ── Set method tests ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_WithReferenceType_ReturnsTrue()
    {
        var result = GlobalStore.Set("key", "value");
        Assert.True(result);
        Assert.True(GlobalStore.Contains("key"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_WithNullValue_Succeeds()
    {
        var result = GlobalStore.Set("nullKey", null);
        Assert.True(result);
        Assert.True(GlobalStore.Contains("nullKey"));
        Assert.Null(GlobalStore.Get("nullKey"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_Overwrites_ExistingValue()
    {
        Assert.True(GlobalStore.Set("key", "original"));
        Assert.True(GlobalStore.Set("key", "updated"));
        Assert.Equal("updated", GlobalStore.Get("key"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_WithValueType_ThrowsWhenNotAllowed()
    {
        _ = Assert.Throws<ArgumentException>(() => GlobalStore.Set("num", 123));
        _ = Assert.Throws<ArgumentException>(() => GlobalStore.Set("flag", true));
        _ = Assert.Throws<ArgumentException>(() => GlobalStore.Set("date", DateTime.Now));
        _ = Assert.Throws<ArgumentException>(() => GlobalStore.Set("guid", Guid.NewGuid()));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_WithValueType_SucceedsWhenAllowed()
    {
        Assert.True(GlobalStore.Set("num", 123, allowsValueType: true));
        Assert.True(GlobalStore.Set("flag", true, allowsValueType: true));
        Assert.True(GlobalStore.Set("date", DateTime.Now, allowsValueType: true));
        Assert.Equal(3, GlobalStore.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1invalid")]
    [InlineData("invalid-name")]
    [InlineData("invalid name")]
    [InlineData("invalid@name")]
    [InlineData("invalid#name")]
    [Trait("Category", "SharedState")]
    public void Set_WithInvalidName_ThrowsArgumentException(string name) => _ = Assert.Throws<ArgumentException>(() => GlobalStore.Set(name, "value"));

    [Theory]
    [InlineData("ValidName")]
    [InlineData("_validName")]
    [InlineData("_123")]
    [InlineData("Name123")]
    [InlineData("Name_With_Underscores")]
    [Trait("Category", "SharedState")]
    public void Set_WithValidName_Succeeds(string name)
    {
        Assert.True(GlobalStore.Set(name, "value"));
        Assert.True(GlobalStore.Contains(name));
    }

    // ── Contains method tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Contains_ExistingKey_ReturnsTrue()
    {
        _ = GlobalStore.Set("exists", "value");
        Assert.True(GlobalStore.Contains("exists"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Contains_MissingKey_ReturnsFalse() => Assert.False(GlobalStore.Contains("missing"));

    [Fact]
    [Trait("Category", "SharedState")]
    public void Contains_CaseInsensitive_ReturnsTrue()
    {
        _ = GlobalStore.Set("KeyName", "value");
        Assert.True(GlobalStore.Contains("keyname"));
        Assert.True(GlobalStore.Contains("KEYNAME"));
        Assert.True(GlobalStore.Contains("KeyName"));
    }

    // ── TryGet method tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_ExistingKey_ReturnsTrue()
    {
        var list = new List<string> { "a", "b", "c" };
        _ = GlobalStore.Set("list", list);
        Assert.True(GlobalStore.TryGet<List<string>>("list", out var result));
        Assert.Same(list, result);
        Assert.Equal(3, result?.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        Assert.False(GlobalStore.TryGet<string>("missing", out var result));
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_WrongType_ReturnsFalse()
    {
        _ = GlobalStore.Set("str", "text");
        Assert.False(GlobalStore.TryGet<int>("str", out var result));
        Assert.Equal(0, result);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_NullValue_ReturnsFalse()
    {
        _ = GlobalStore.Set("nullVal", null);
        // TryGet returns false for null values because null is not an instance of any type
        Assert.False(GlobalStore.TryGet<string?>("nullVal", out var result));
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void TryGet_CaseInsensitive_Works()
    {
        _ = GlobalStore.Set("KeyName", "value");
        Assert.True(GlobalStore.TryGet<string>("keyname", out var result));
        Assert.Equal("value", result);
    }

    // ── Get method tests ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Get_ExistingKey_ReturnsValue()
    {
        _ = GlobalStore.Set("key", "value");
        Assert.Equal("value", GlobalStore.Get("key"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Get_MissingKey_ReturnsNull() => Assert.Null(GlobalStore.Get("missing"));

    [Fact]
    [Trait("Category", "SharedState")]
    public void Get_CaseInsensitive_Works()
    {
        _ = GlobalStore.Set("KeyName", "value");
        Assert.Equal("value", GlobalStore.Get("keyname"));
        Assert.Equal("value", GlobalStore.Get("KEYNAME"));
    }

    // ── Snapshot method tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Snapshot_ReturnsAllEntries()
    {
        _ = GlobalStore.Set("key1", "val1");
        _ = GlobalStore.Set("key2", "val2");
        _ = GlobalStore.Set("key3", "val3");

        var snapshot = GlobalStore.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal("val1", snapshot["key1"]);
        Assert.Equal("val2", snapshot["key2"]);
        Assert.Equal("val3", snapshot["key3"]);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Snapshot_ReturnsShallowCopy()
    {
        _ = GlobalStore.Set("key1", "val1");
        var snapshot = GlobalStore.Snapshot();
        _ = Assert.Single(snapshot);

        // Add after snapshot
        _ = GlobalStore.Set("key2", "val2");

        // Snapshot should still be 1 item
        _ = Assert.Single(snapshot);
        Assert.Equal(2, GlobalStore.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Snapshot_EmptyStore_ReturnsEmptyDictionary()
    {
        var snapshot = GlobalStore.Snapshot();
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot);
    }

    // ── KeySnapshot method tests ────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void KeySnapshot_ReturnsAllKeys()
    {
        _ = GlobalStore.Set("alpha", "a");
        _ = GlobalStore.Set("beta", "b");
        _ = GlobalStore.Set("gamma", "c");

        var keys = GlobalStore.KeySnapshot();
        Assert.Equal(3, keys.Count);
        Assert.Contains("alpha", keys);
        Assert.Contains("beta", keys);
        Assert.Contains("gamma", keys);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void KeySnapshot_EmptyStore_ReturnsEmptyCollection()
    {
        var keys = GlobalStore.KeySnapshot();
        Assert.NotNull(keys);
        Assert.Empty(keys);
    }

    // ── Clear method tests ────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Clear_RemovesAllEntries()
    {
        _ = GlobalStore.Set("key1", "val1");
        _ = GlobalStore.Set("key2", "val2");
        _ = GlobalStore.Set("key3", "val3");
        Assert.Equal(3, GlobalStore.Count);

        GlobalStore.Clear();
        Assert.Equal(0, GlobalStore.Count);
        Assert.False(GlobalStore.Contains("key1"));
        Assert.False(GlobalStore.Contains("key2"));
        Assert.False(GlobalStore.Contains("key3"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Clear_EmptyStore_DoesNotThrow()
    {
        GlobalStore.Clear();
        Assert.Equal(0, GlobalStore.Count);

        // Should not throw
        GlobalStore.Clear();
        Assert.Equal(0, GlobalStore.Count);
    }

    // ── Count property tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Count_ReflectsCurrentSize()
    {
        Assert.Equal(0, GlobalStore.Count);

        _ = GlobalStore.Set("key1", "val1");
        Assert.Equal(1, GlobalStore.Count);

        _ = GlobalStore.Set("key2", "val2");
        Assert.Equal(2, GlobalStore.Count);

        _ = GlobalStore.Remove("key1");
        Assert.Equal(1, GlobalStore.Count);

        GlobalStore.Clear();
        Assert.Equal(0, GlobalStore.Count);
    }

    // ── Keys property tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Keys_ReturnsAllKeys()
    {
        _ = GlobalStore.Set("first", "1");
        _ = GlobalStore.Set("second", "2");
        _ = GlobalStore.Set("third", "3");

        var keys = GlobalStore.Keys.ToList();
        Assert.Equal(3, keys.Count);
        Assert.Contains("first", keys);
        Assert.Contains("second", keys);
        Assert.Contains("third", keys);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Keys_EmptyStore_ReturnsEmpty()
    {
        var keys = GlobalStore.Keys.ToList();
        Assert.Empty(keys);
    }

    // ── Values property tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Values_ReturnsAllValues()
    {
        _ = GlobalStore.Set("key1", "val1");
        _ = GlobalStore.Set("key2", "val2");
        _ = GlobalStore.Set("key3", "val3");

        var values = GlobalStore.Values.ToList();
        Assert.Equal(3, values.Count);
        Assert.Contains("val1", values);
        Assert.Contains("val2", values);
        Assert.Contains("val3", values);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Values_WithNullValue_IncludesNull()
    {
        _ = GlobalStore.Set("key1", "val1");
        _ = GlobalStore.Set("key2", null);

        var values = GlobalStore.Values.ToList();
        Assert.Equal(2, values.Count);
        Assert.Contains("val1", values);
        Assert.Contains(null, values);
    }

    // ── Remove method tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Remove_ExistingKey_ReturnsTrueAndRemoves()
    {
        _ = GlobalStore.Set("key", "value");
        Assert.True(GlobalStore.Contains("key"));

        Assert.True(GlobalStore.Remove("key"));
        Assert.False(GlobalStore.Contains("key"));
        Assert.Equal(0, GlobalStore.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Remove_MissingKey_ReturnsFalse() => Assert.False(GlobalStore.Remove("missing"));

    [Fact]
    [Trait("Category", "SharedState")]
    public void Remove_CaseInsensitive_Works()
    {
        _ = GlobalStore.Set("KeyName", "value");
        Assert.True(GlobalStore.Remove("keyname"));
        Assert.False(GlobalStore.Contains("KeyName"));
        Assert.False(GlobalStore.Contains("keyname"));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Remove_AfterClear_ReturnsFalse()
    {
        _ = GlobalStore.Set("key", "value");
        GlobalStore.Clear();
        Assert.False(GlobalStore.Remove("key"));
    }

    // ── concurrent access tests ────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public async Task ConcurrentSet_MaintainsThreadSafety()
    {
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => GlobalStore.Set($"key{index}", $"val{index}")));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(100, GlobalStore.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public async Task ConcurrentRemove_MaintainsThreadSafety()
    {
        // Populate
        for (var i = 0; i < 100; i++)
        {
            _ = GlobalStore.Set($"key{i}", $"val{i}");
        }

        var tasks = new List<Task>();
        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => GlobalStore.Remove($"key{index}")));
        }

        await Task.WhenAll(tasks);
        Assert.Equal(0, GlobalStore.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public async Task ConcurrentReadWrite_MaintainsConsistency()
    {
        _ = GlobalStore.Set("counter", new List<int>());

        var tasks = new List<Task>();

        // Mix reads and writes
        for (var i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() => GlobalStore.Set($"key{i}", $"val{i}")));
            tasks.Add(Task.Run(() => _ = GlobalStore.Get($"key{i}")));
        }

        await Task.WhenAll(tasks);

        // At least some keys should exist (writes may complete after reads)
        Assert.True(GlobalStore.Count > 0);
    }

    // ── complex object tests ────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_ComplexObject_PreservesReference()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        _ = GlobalStore.Set("dict", dict);

        Assert.True(GlobalStore.TryGet<Dictionary<string, int>>("dict", out var result));
        Assert.Same(dict, result);

        // Modify original
        dict["c"] = 3;

        // Retrieved object should reflect changes (same reference)
        Assert.True(GlobalStore.TryGet<Dictionary<string, int>>("dict", out var updated));
        Assert.Equal(3, updated?.Count);
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_MultipleTypes_StoresCorrectly()
    {
        var list = new List<string> { "a", "b" };
        var dict = new Dictionary<string, int> { ["x"] = 1 };
        var array = new[] { 1, 2, 3 };

        _ = GlobalStore.Set("list", list);
        _ = GlobalStore.Set("dict", dict);
        _ = GlobalStore.Set("array", array);

        Assert.True(GlobalStore.TryGet<List<string>>("list", out var gotList));
        Assert.True(GlobalStore.TryGet<Dictionary<string, int>>("dict", out var gotDict));
        Assert.True(GlobalStore.TryGet<int[]>("array", out var gotArray));

        Assert.Same(list, gotList);
        Assert.Same(dict, gotDict);
        Assert.Same(array, gotArray);
    }

    // ── edge cases ────────────────────────────────────────────────
    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_LongName_Succeeds()
    {
        var longName = new string('a', 1000);
        Assert.True(GlobalStore.Set(longName, "value"));
        Assert.True(GlobalStore.Contains(longName));
    }

    [Fact]
    [Trait("Category", "SharedState")]
    public void Set_UnicodeInValue_Succeeds()
    {
        _ = GlobalStore.Set("unicode", "Hello 世界 🌍");
        Assert.Equal("Hello 世界 🌍", GlobalStore.Get("unicode"));
    }
}
