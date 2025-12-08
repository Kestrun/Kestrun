using Kestrun.Utilities;
using System.Collections;
using Xunit;

namespace KestrunTests.Utilities;

public class ObjectToDictionaryConverterTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionary_WithHashtable_ConvertsDictionaryKeysAndValues()
    {
        var input = new Hashtable
        {
            ["key1"] = "value1",
            ["key2"] = 42,
            ["key3"] = null
        };

        var result = ObjectToDictionaryConverter.ToDictionary(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("value1", result["key1"]);
        Assert.Equal("42", result["key2"]);
        Assert.Equal("", result["key3"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionary_WithGenericDictionary_ConvertsDictionaryKeysAndValues()
    {
        var input = new Dictionary<string, object?>
        {
            ["name"] = "Alice",
            ["age"] = 30,
            ["email"] = null
        };

        var result = ObjectToDictionaryConverter.ToDictionary(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("Alice", result["name"]);
        Assert.Equal("30", result["age"]);
        Assert.Equal("", result["email"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionary_WithEnumerable_CreatesIndexedKeys()
    {
        var input = new[] { "apple", "banana", "cherry" };

        var result = ObjectToDictionaryConverter.ToDictionary(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("apple", result["item[0]"]);
        Assert.Equal("banana", result["item[1]"]);
        Assert.Equal("cherry", result["item[2]"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionary_WithObjectWithProperties_ExtractsPublicProperties()
    {
        var input = new TestObject { Name = "Bob", Value = 99, Email = "bob@example.com" };

        var result = ObjectToDictionaryConverter.ToDictionary(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("Bob", result["Name"]);
        Assert.Equal("99", result["Value"]);
        Assert.Equal("bob@example.com", result["Email"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionary_WithNull_ReturnsEmptyDictionary()
    {
        var result = ObjectToDictionaryConverter.ToDictionary(null);

        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionary_WithEmptyEnumerable_ReturnsEmptyDictionary()
    {
        var input = new int[] { };

        var result = ObjectToDictionaryConverter.ToDictionary(input);

        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionary_WithString_TreatsAsObject()
    {
        var input = "hello world";

        var result = ObjectToDictionaryConverter.ToDictionary(input);

        // String should be treated as an object with properties, not as an enumerable
        Assert.NotEmpty(result);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionaryObject_WithHashtable_PreservesObjectValues()
    {
        var expectedObj = new object();
        var input = new Hashtable
        {
            ["key1"] = "value1",
            ["key2"] = 42,
            ["obj"] = expectedObj,
            ["null"] = null
        };

        var result = ObjectToDictionaryConverter.ToDictionaryObject(input);

        Assert.Equal(4, result.Count);
        Assert.Equal("value1", result["key1"]);
        Assert.Equal(42, result["key2"]);
        Assert.Same(expectedObj, result["obj"]);
        Assert.Null(result["null"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionaryObject_WithEnumerable_CreatesIndexedKeysWithOriginalTypes()
    {
        var input = new object[] { "string", 42, 3.14, true };

        var result = ObjectToDictionaryConverter.ToDictionaryObject(input);

        Assert.Equal(4, result.Count);
        Assert.Equal("string", result["item[0]"]);
        Assert.Equal(42, result["item[1]"]);
        Assert.Equal(3.14, result["item[2]"]);
        Assert.Equal(true, result["item[3]"]);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ToDictionaryObject_WithObjectWithProperties_PreservesPropertyTypes()
    {
        var input = new TestObjectWithTypes { Name = "Charlie", Count = 100, Active = true };

        var result = ObjectToDictionaryConverter.ToDictionaryObject(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("Charlie", result["Name"]);
        Assert.Equal(100, result["Count"]);
        Assert.True((bool)result["Active"]!);
    }

    // Test helper classes
    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    private class TestObjectWithTypes
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool Active { get; set; }
    }
}
