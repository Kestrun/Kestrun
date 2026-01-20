using System.Collections;
using System.Reflection;
using System.Text.Json;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorTagsTests
{
    private static JsonElement GetTagByName(JsonElement root, string name)
    {
        if (!root.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            throw new Xunit.Sdk.XunitException("OpenAPI document did not contain a 'tags' array.");
        }

        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.TryGetProperty("name", out var n) && string.Equals(n.GetString(), name, StringComparison.Ordinal))
            {
                return tag;
            }
        }

        throw new Xunit.Sdk.XunitException($"Tag '{name}' not found in OpenAPI document.");
    }

    private static string? ReadParentName(JsonElement tag)
    {
        return tag.TryGetProperty("parent", out var parent)
            ? parent.ValueKind switch
            {
                JsonValueKind.String => parent.GetString(),
                JsonValueKind.Object => ReadParentFromObject(parent),
                _ => null,
            }
            : null;

        static string? ReadParentFromObject(JsonElement parent)
        {
            if (parent.TryGetProperty("name", out var name))
            {
                return name.GetString();
            }

            if (parent.TryGetProperty("$ref", out var @ref))
            {
                var refStr = @ref.GetString();
                if (string.IsNullOrWhiteSpace(refStr))
                {
                    return null;
                }

                // Common shape is something like "#/tags/operations".
                var lastSlash = refStr.LastIndexOf('/', refStr.Length - 1);
                return lastSlash >= 0 ? refStr[(lastSlash + 1)..] : refStr;
            }

            return null;
        }
    }

    [Fact]
    public void AddTag_SerializesHierarchyExternalDocsAndExtensions()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var docsExtensions = new Hashtable
        {
            ["x-audience"] = "public",
            ["x-docType"] = "reference",
            ["x-nullValue"] = null,
        };

        var extDocs = d.CreateExternalDocs(
            new Uri("https://example.com/orders", UriKind.Absolute),
            description: "Order docs",
            extensions: docsExtensions);

        var tagExtensions = new Hashtable
        {
            ["x-displayName"] = "Orders",
            ["x-sortOrder"] = 10,
            ["x-icon"] = "box",
            ["x-nullValue"] = null,
            [""] = "ignored",
        };

        _ = d.AddTag(
            name: "orders",
            description: "Order operations",
            summary: "Orders summary",
            parent: "operations",
            kind: "resource",
            externalDocs: extDocs,
            extensions: tagExtensions);

        using var jsonDoc = JsonDocument.Parse(d.ToJson(OpenApiSpecVersion.OpenApi3_2));
        var root = jsonDoc.RootElement;

        var orders = GetTagByName(root, "orders");

        Assert.Equal("Order operations", orders.GetProperty("description").GetString());
        Assert.Equal("Orders summary", orders.GetProperty("summary").GetString());
        Assert.Equal("resource", orders.GetProperty("kind").GetString());
        Assert.Equal("operations", ReadParentName(orders));

        Assert.Equal("Orders", orders.GetProperty("x-displayName").GetString());
        Assert.Equal(10, orders.GetProperty("x-sortOrder").GetInt32());
        Assert.Equal("box", orders.GetProperty("x-icon").GetString());
        Assert.False(orders.TryGetProperty("x-nullValue", out _));

        var externalDocs = orders.GetProperty("externalDocs");
        Assert.Equal("https://example.com/orders", externalDocs.GetProperty("url").GetString());
        Assert.Equal("Order docs", externalDocs.GetProperty("description").GetString());
        Assert.Equal("public", externalDocs.GetProperty("x-audience").GetString());
        Assert.Equal("reference", externalDocs.GetProperty("x-docType").GetString());
    }

    [Fact]
    public void AddTag_WhenCalledTwice_ReturnsSameInstance_AndWhitespaceDoesNotOverwrite()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var t1 = d.AddTag("orders", description: "first");
        var t2 = d.AddTag("orders", description: " ");

        Assert.Same(t1, t2);
        Assert.Equal("first", t2.Description);
    }

    [Fact]
    public void TryGetTag_ReturnsFalseWhenMissing_TrueWhenPresent()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        Assert.False(d.TryGetTag("missing", out _));

        _ = d.AddTag("orders", description: "Order operations");
        Assert.True(d.TryGetTag("orders", out var tag));
        var nonNullTag = Assert.IsType<OpenApiTag>(tag);
        Assert.Equal("Order operations", nonNullTag.Description);
    }

    [Fact]
    public void RemoveTag_RemovesByNameEquality()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = d.AddTag("orders");

        // Remove using a different instance that compares equal.
        Assert.True(d.RemoveTag(new OpenApiTag { Name = "orders" }));
        Assert.False(d.TryGetTag("orders", out _));

        // Removing again should return false.
        Assert.False(d.RemoveTag(new OpenApiTag { Name = "orders" }));
    }

    [Fact]
    public void CreateExternalDocs_StringOverload_ThrowsOnWhitespace()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<ArgumentException>(() => d.CreateExternalDocs(" ", description: "desc"));
    }

    [Fact]
    public void CreateExternalDocs_UriOverload_NormalizesExtensions_AndSkipsNullValues()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var extensions = new Hashtable
        {
            ["x-audience"] = "public",
            ["x-docType"] = "reference",
            ["x-nullValue"] = null,
            [" "] = "ignored",
        };

        var docs = d.CreateExternalDocs(
            new Uri("https://example.com", UriKind.Absolute),
            description: "Example",
            extensions: extensions);

        var url = Assert.IsType<Uri>(docs.Url);
        Assert.Equal("https://example.com/", url.ToString());
        Assert.Equal("Example", docs.Description);

        var ext = Assert.IsAssignableFrom<IDictionary<string, IOpenApiExtension>>(docs.Extensions);
        Assert.True(ext.ContainsKey("x-audience"));
        Assert.True(ext.ContainsKey("x-docType"));
        Assert.False(ext.ContainsKey("x-nullValue"));
    }

    [Fact]
    public void PrivateHelpers_AddTagIfMissing_RemoveTagByName_GetOrCreateTagItem_Work()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var addIfMissing = typeof(OpenApiDocDescriptor).GetMethod(
            "AddTagIfMissing",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected private method 'AddTagIfMissing' was not found.");

        var removeByName = typeof(OpenApiDocDescriptor).GetMethod(
            "RemoveTag",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string)],
            modifiers: null)
            ?? throw new InvalidOperationException("Expected private method 'RemoveTag(string)' was not found.");

        var getOrCreate = typeof(OpenApiDocDescriptor).GetMethod(
            "GetOrCreateTagItem",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected private method 'GetOrCreateTagItem' was not found.");

        var tag = new OpenApiTag { Name = "orders" };
        Assert.True((bool)addIfMissing.Invoke(d, [tag])!);
        Assert.False((bool)addIfMissing.Invoke(d, [tag])!);

        var t1 = Assert.IsType<OpenApiTag>(getOrCreate.Invoke(d, ["orders"])!);
        var t2 = Assert.IsType<OpenApiTag>(getOrCreate.Invoke(d, ["orders"])!);
        Assert.Same(t1, t2);

        Assert.True((bool)removeByName!.Invoke(d, ["orders"])!);
        Assert.False((bool)removeByName.Invoke(d, ["orders"])!);
    }
}
