using Kestrun.Hosting;
using Kestrun.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public class OpenApiAttributeTests
{
    static OpenApiAttributeTests() =>
        // Ensure a logger exists (tests may already configure this globally)
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    // --- Test component models -------------------------------------------------

    // Schema component used by responses/headers via SchemaRef
    [OpenApiSchemaComponent(Title = "Test Payload", Description = "Payload schema for testing")]
    private class TestPayload
    {
        public string Name { get; set; } = "Alice";
        public int Age { get; set; } = 30;
    }

    // Example component referenced by example refs
    [OpenApiExampleComponent(Key = "UserEx", Summary = "User example", Description = "Example user payload")]
    private class UserExample
    {
        public string Name { get; set; } = "Alice";
    }

    // Response holder with inline vs reference schema and example refs
    [OpenApiResponseComponent(Description = "Default response description")]
    private class ResponseHolder
    {
        // Use object type to prevent automatic schema inference override
        [OpenApiResponse(Description = "Inline schema", SchemaRef = "TestPayload", ContentType = ["application/json"], Inline = true)]
        [OpenApiExampleRef(Key = "exClone", ReferenceId = "UserEx", ContentType = "application/json", Inline = true)]
        [OpenApiExampleRef(Key = "exRef", ReferenceId = "UserEx", ContentType = "application/json", Inline = false)]
        public object Inline { get; set; } = new();

        [OpenApiResponse(Description = "Referenced schema", SchemaRef = "TestPayload", ContentType = ["application/json"], Inline = false)]
        public object Ref { get; set; } = new();
    }

    // Header holder with header attribute + inline & reference examples
    [OpenApiHeaderComponent(Description = "Default header description")]
    private class HeaderHolder
    {
        [OpenApiHeader(Key = "X-Custom", Description = "Custom header", Required = true, Deprecated = true, AllowEmptyValue = true, AllowReserved = true, Explode = true, Type = "string", SchemaRef = "TestPayload", Example = "hello")]
        [OpenApiExampleRef(Key = "hdrExRef", ReferenceId = "UserEx")]
        [OpenApiExample(Key = "hdrInline", Summary = "Inline summary", Description = "Inline desc", Value = "inlineVal")]
        public string Custom { get; set; } = "default";
    }

    // ---------------------------------------------------------------------------

    [Fact]
    public void ResponseAttribute_SchemaRef_InlineVsRef_BehavesAsExpected()
    {
        var host = new KestrunHost("TestApp", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument("doc1");
        var set = new OpenApiComponentSet
        {
            SchemaTypes = [typeof(TestPayload)],
            ResponseTypes = [typeof(ResponseHolder)],
            ExampleTypes = [typeof(UserExample)]
        };

        descriptor.GenerateComponents(set);

        Assert.NotNull(descriptor.Document.Components);
        var responses = descriptor.Document.Components.Responses;
        Assert.NotNull(responses);
        Assert.Contains("Inline", responses.Keys);
        Assert.Contains("Ref", responses.Keys);

        var inlineResp = (Microsoft.OpenApi.OpenApiResponse)responses["Inline"];
        var refResp = (Microsoft.OpenApi.OpenApiResponse)responses["Ref"];

        // Inline response: ensure content dictionary and media type present
        Assert.NotNull(inlineResp.Content);
        Assert.True(inlineResp.Content.TryGetValue("application/json", out var mediaType));
        var inlineSchema = mediaType.Schema;
        var concrete = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(inlineSchema);
        // Ensure clone (new instance) vs referenced component
        Assert.NotNull(descriptor.Document.Components.Schemas);
        Assert.False(ReferenceEquals(concrete, descriptor.Document.Components.Schemas["TestPayload"]));
        // Basic property presence check on cloned schema
        if (concrete.Properties is { Count: > 0 })
        {
            Assert.Contains("Name", concrete.Properties.Keys);
            Assert.Contains("Age", concrete.Properties.Keys);
        }

        // Examples: exClone (inline) -> concrete; exRef -> reference
        var examples = inlineResp.Content["application/json"].Examples;
        Assert.NotNull(examples);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiExample>(examples["exClone"]);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiExampleReference>(examples["exRef"]);

        // Referenced response: schema is a reference wrapper
        Assert.NotNull(refResp.Content);
        Assert.True(refResp.Content.TryGetValue("application/json", out mediaType));
        var refSchema = mediaType.Schema;
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiSchemaReference>(refSchema);
    }

    [Fact]
    public void HeaderAttribute_ExamplesAndSchemaRef_AppliedCorrectly()
    {
        var host = new KestrunHost("TestApp", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument("doc2");
        var set = new OpenApiComponentSet
        {
            SchemaTypes = [typeof(TestPayload)],
            HeaderTypes = [typeof(HeaderHolder)],
            ExampleTypes = [typeof(UserExample)]
        };
        descriptor.GenerateComponents(set);

        Assert.NotNull(descriptor.Document.Components);
        var headers = descriptor.Document.Components.Headers;
        Assert.NotNull(headers);
        Assert.Contains("X-Custom", headers.Keys); // Key override from attribute

        var header = (Microsoft.OpenApi.OpenApiHeader)headers["X-Custom"];
        Assert.Equal("Custom header", header.Description); // Description from attribute
        Assert.True(header.Required);
        Assert.True(header.Deprecated);
        Assert.True(header.AllowEmptyValue);
        Assert.True(header.AllowReserved);
        Assert.True(header.Explode);
        Assert.Equal("hello", header.Example?.ToString());

        // Schema reference applied
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiSchemaReference>(header.Schema);

        // Examples: reference + inline
        Assert.NotNull(header.Examples);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiExampleReference>(header.Examples["hdrExRef"]);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiExample>(header.Examples["hdrInline"]);
        var inlineEx = (Microsoft.OpenApi.OpenApiExample)header.Examples["hdrInline"];
        Assert.Equal("Inline summary", inlineEx.Summary);
        Assert.Equal("Inline desc", inlineEx.Description);
        Assert.Equal("inlineVal", inlineEx.Value?.ToString());
    }
}
