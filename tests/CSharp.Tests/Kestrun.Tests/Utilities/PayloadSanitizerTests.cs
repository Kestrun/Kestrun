using System.Collections;
using System.Management.Automation;
using System.Text.Json;
using Kestrun.Utilities.Json;
using Xunit;

namespace KestrunTests.Utilities;

public class PayloadSanitizerTests
{
    [Fact]
    public void TimeSpan_Is_Normalized_To_C_Format()
    {
        var ts = new TimeSpan(1, 2, 3, 4, 5);
        var sanitized = PayloadSanitizer.Sanitize(ts);
        var s = Assert.IsType<string>(sanitized);
        Assert.Equal("1.02:03:04.0050000", s);
    }

    [Fact]
    public void Dictionary_SelfReference_Replaced_With_Circular()
    {
        var dict = new Dictionary<string, object?>();
        dict["self"] = dict; // self-reference

        var sanitized = PayloadSanitizer.Sanitize(dict);

        var result = Assert.IsType<Dictionary<string, object?>>(sanitized);
        Assert.True(result.ContainsKey("self"));
        Assert.Equal("[Circular]", result["self"]);
    }

    [Fact]
    public void Enumerable_Elements_Are_Sanitized_Recursively()
    {
        var list = new ArrayList { 1, TimeSpan.FromMilliseconds(1234) };
        var sanitized = PayloadSanitizer.Sanitize(list);

        var arr = Assert.IsType<List<object?>>(sanitized);
        Assert.Equal(2, arr.Count);
        Assert.Equal(1, arr[0]);
        Assert.Equal("00:00:01.2340000", arr[1]);
    }

    [Fact]
    public void Exception_Is_Projected_To_Friendly_Object()
    {
        try
        {
            _ = 1 / int.Parse("0");
        }
        catch (Exception ex)
        {
            var sanitized = PayloadSanitizer.Sanitize(ex);
            var json = JsonSerializer.Serialize(sanitized);
            Assert.Contains("\"Type\":\"System.DivideByZeroException\"", json);
            Assert.Contains("\"Message\":", json);
            Assert.Contains("\"StackTrace\":", json);
        }
    }

    [Fact]
    public void PSObject_CustomObject_Unwraps_To_Dictionary_And_Skips_Specials()
    {
        // Create a PSCustomObject-like PSObject in C# by setting the type name
        var pso = new PSObject();
        // Signal it is a PSCustomObject to our sanitizer
        pso.TypeNames.Insert(0, "System.Management.Automation.PSCustomObject");
        pso.Properties.Add(new PSNoteProperty("Name", "Alice"));
        pso.Properties.Add(new PSNoteProperty("Value", 42));

        var sanitized = PayloadSanitizer.Sanitize(pso);
        var dict = Assert.IsType<Dictionary<string, object?>>(sanitized);

        Assert.Equal("Alice", dict["Name"]);
        Assert.Equal(42, dict["Value"]);

        // Ensure special meta-members are not included
        var specials = new[] { "PSObject", "BaseObject", "Members", "ImmediateBaseObject", "Properties", "TypeNames", "Methods" };
        foreach (var s in specials)
        {
            Assert.False(dict.ContainsKey(s));
        }
    }
}
