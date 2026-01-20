using System.Collections;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorHeadersTests
{
    [Fact]
    public void NewOpenApiHeader_DefaultsToStringSchema_WhenNoSchemaAndNoContent()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var header = d.NewOpenApiHeader();

        var schema = Assert.IsType<OpenApiSchema>(header.Schema);
        Assert.Equal(JsonSchemaType.String, schema.Type);
    }

    [Fact]
    public void NewOpenApiHeader_Throws_WhenSchemaAndContentProvided()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<InvalidOperationException>(() =>
            d.NewOpenApiHeader(schema: typeof(string), content: new Hashtable { { "application/json", new OpenApiMediaType() } }));
    }

    [Fact]
    public void NewOpenApiHeader_Throws_WhenExamplesKeyNotString()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<InvalidOperationException>(() =>
            d.NewOpenApiHeader(examples: new Hashtable { { 123, new OpenApiExample() } }));
    }

    [Fact]
    public void NewOpenApiHeader_Throws_WhenExamplesValueInvalid()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<InvalidOperationException>(() =>
            d.NewOpenApiHeader(examples: new Hashtable { { "ex", 123 } }));
    }

    [Fact]
    public void NewOpenApiHeader_ResolvesExampleString_ToReferenceFromComponents()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddComponentExample("ex1", new OpenApiExample { Summary = "component" });

        var header = d.NewOpenApiHeader(examples: new Hashtable { { "one", "ex1" } });

        var ex = Assert.IsType<OpenApiExampleReference>(header.Examples!["one"]);
        Assert.Equal("ex1", ex.Reference.Id);
    }

    [Fact]
    public void NewOpenApiHeader_ResolvesExampleString_ToCloneFromInline()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddInlineExample("ex2", new OpenApiExample { Summary = "inline" });

        var header = d.NewOpenApiHeader(examples: new Hashtable { { "one", "ex2" } });

        Assert.True(d.TryGetInline<OpenApiExample>("ex2", OpenApiComponentKind.Examples, out var inlineExample));
        var added = Assert.IsType<OpenApiExample>(header.Examples!["one"]);
        Assert.Equal("inline", added.Summary);
        Assert.NotSame(inlineExample, added);
    }

    [Fact]
    public void NewOpenApiHeader_ContentReference_ClonesFromComponents_AndDoesNotSetSchema()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.Document.Components ??= new OpenApiComponents();
        d.Document.Components.MediaTypes ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        var media = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        d.Document.Components.MediaTypes["mt1"] = media;

        var header = d.NewOpenApiHeader(content: new Hashtable { { "application/json", "mt1" } });

        Assert.Null(header.Schema);
        var stored = Assert.IsType<OpenApiMediaType>(header.Content!["application/json"]);
        Assert.NotSame(media, stored);
    }
}
