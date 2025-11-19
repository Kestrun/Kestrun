using Kestrun.Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace KestrunTests.Certificates;

public class JwkJsonTests
{
    [Fact]
    [Trait("Category", "Certificates")]
    public void Options_AreConfiguredForJwkSerialization()
    {
        var opts = JwkJson.Options;
        Assert.NotNull(opts);
        Assert.Equal(JsonNamingPolicy.CamelCase, opts.PropertyNamingPolicy);
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, opts.DefaultIgnoreCondition);
        Assert.False(opts.WriteIndented);
    }

    private sealed class TestModel
    {
        public string? Kty { get; set; }
        public string? N { get; set; }
        public string? E { get; set; }
        public string? Optional { get; set; }
    }

    [Fact]
    [Trait("Category", "Certificates")]
    public void Serialization_UsesCamelCase_And_IgnoresNulls()
    {
        var model = new TestModel
        {
            Kty = "RSA",
            N = "mod",
            E = "AQAB",
            Optional = null
        };

        var json = JsonSerializer.Serialize(model, JwkJson.Options);

        Assert.Contains("\"kty\":\"RSA\"", json);
        Assert.Contains("\"n\":\"mod\"", json);
        Assert.Contains("\"e\":\"AQAB\"", json);
        Assert.DoesNotContain("optional", json); // null ignored
    }
}
