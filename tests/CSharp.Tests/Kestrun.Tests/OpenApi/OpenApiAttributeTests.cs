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

    [OpenApiHeader(
        Key = "X-Custom",
        Description = "Custom header",
        Required = true,
        Deprecated = true,
        AllowEmptyValue = true,
        Explode = true,
        AllowReserved = true,
        Type = "string",
        SchemaRef = "TestPayload",
        ExampleRef = "UserEx",
        Example = "inlineVal")]
    private class CustomHeaderComponent
    {
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


    // ---------------------------------------------------------------------------

    [Fact]
    public void ResponseAttribute_SchemaRef_InlineVsRef_BehavesAsExpected()
    {
        var host = new KestrunHost("TestApp", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument("doc1");

        descriptor.AddComponentExample(
            "UserEx",
            new Microsoft.OpenApi.OpenApiExample
            {
                Summary = "User example",
                Description = "Example used by tests",
                Value = OpenApiDocDescriptor.ToNode(new { Name = "Alice", Age = 30 })
            });

        var set = new OpenApiComponentSet
        {
            SchemaTypes = [typeof(TestPayload)],
            ResponseTypes = [typeof(ResponseHolder)]
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

}
