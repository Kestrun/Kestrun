using System.Collections;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Serilog;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorExamplesTests
{
    [Fact]
    public void NewOpenApiExample_WithValue_SetsValueAndExtensions()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var extensions = new Hashtable
        {
            ["x-demo"] = new { enabled = true },
            ["invalid"] = "ignored"
        };

        var example = descriptor.NewOpenApiExample(
            summary: "sample",
            description: "example payload",
            value: new { id = 7 },
            extensions: extensions);

        Assert.Equal("sample", example.Summary);
        Assert.Equal("example payload", example.Description);
        Assert.NotNull(example.Value);
        Assert.NotNull(example.Extensions);
        Assert.True(example.Extensions.ContainsKey("x-demo"));
        Assert.False(example.Extensions.ContainsKey("invalid"));
    }

    [Fact]
    public void NewOpenApiExternalExample_SetsExternalValue()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var example = descriptor.NewOpenApiExternalExample(
            summary: "external",
            description: "",
            externalValue: "https://example.test/examples/payload.json",
            extensions: null);

        Assert.Equal("external", example.Summary);
        Assert.Null(example.Description);
        Assert.Equal("https://example.test/examples/payload.json", example.ExternalValue);
    }

    [Fact]
    public void NewOpenApiExample_WithDataValueAndSerializedValue_SetsBoth()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var example = descriptor.NewOpenApiExample(
            summary: "serialized",
            description: "desc",
            dataValue: new { code = 200 },
            serializedValue: /*lang=json,strict*/ "{\"code\":200}",
            extensions: null);

        Assert.Equal("serialized", example.Summary);
        Assert.Equal("desc", example.Description);
        Assert.NotNull(example.DataValue);
        Assert.Equal(/*lang=json,strict*/ "{\"code\":200}", example.SerializedValue);
    }
}
