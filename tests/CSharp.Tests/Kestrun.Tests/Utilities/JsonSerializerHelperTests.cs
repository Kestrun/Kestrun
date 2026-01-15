using System.Text.Json;
using Kestrun.Utilities.Json;
using Xunit;

namespace KestrunTests.Utilities;

public class JsonSerializerHelperTests
{
    private sealed class Demo
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void FromJson_Generic_Is_CaseInsensitive()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string> { ["NAME"] = "bob" });
        var obj = JsonSerializerHelper.FromJson<Demo>(json);
        Assert.Equal("bob", obj.Name);
    }

    [Fact]
    public void FromJson_Generic_Throws_On_Null_Payload()
    {
        var ex = Assert.Throws<JsonException>(() => JsonSerializerHelper.FromJson<Demo>("null"));
        Assert.Contains("Deserialization of type", ex.Message);
    }

    [Fact]
    public void FromJson_Generic_Throws_On_Invalid_Json()
        => Assert.Throws<JsonException>(() => JsonSerializerHelper.FromJson<Demo>("{bad json"));

    [Fact]
    public void FromJson_NonGeneric_Deserializes_To_Specified_Type()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, int> { ["a"] = 1 });
        var obj = JsonSerializerHelper.FromJson(json, typeof(Dictionary<string, int>));
        var dict = Assert.IsType<Dictionary<string, int>>(obj);
        Assert.Equal(1, dict["a"]);
    }

    [Fact]
    public void FromJson_NonGeneric_Throws_On_Null_Payload()
        => Assert.Throws<JsonException>(() => JsonSerializerHelper.FromJson("null", typeof(Demo)));
}
