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

    private sealed class DemoProduct
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Stock { get; set; }
    }

    [Fact]
    public void PSObject_Wrapped_ClrObject_Unwraps_To_BaseObject()
    {
        var product = new DemoProduct { Id = 101, Name = "Laptop Pro", Price = 1299.99, Stock = 4 };
        var pso = new PSObject(product);

        var sanitized = PayloadSanitizer.Sanitize(pso);

        var unwrapped = Assert.IsType<DemoProduct>(sanitized);
        Assert.Equal(101, unwrapped.Id);
        Assert.Equal("Laptop Pro", unwrapped.Name);
    }

    [Fact]
    public void Enumerable_Of_PSObject_Wrapped_ClrObjects_Unwraps_To_List_Of_BaseObjects()
    {
        var p1 = new PSObject(new DemoProduct { Id = 101, Name = "Laptop Pro", Price = 1299.99, Stock = 4 });
        var p2 = new PSObject(new DemoProduct { Id = 102, Name = "Laptop Air", Price = 999.00, Stock = 0 });

        var sanitized = PayloadSanitizer.Sanitize(new[] { p1, p2 });

        var list = Assert.IsType<List<object?>>(sanitized);
        Assert.Equal(2, list.Count);
        _ = Assert.IsType<DemoProduct>(list[0]);
        _ = Assert.IsType<DemoProduct>(list[1]);

        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"id\":101", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"members\"", json);
        Assert.DoesNotContain("\"properties\"", json);
        Assert.DoesNotContain("\"methods\"", json);
    }
}
