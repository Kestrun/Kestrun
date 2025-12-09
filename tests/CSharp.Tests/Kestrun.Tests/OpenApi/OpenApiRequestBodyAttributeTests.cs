using Kestrun.Hosting;
using Kestrun.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public class OpenApiRequestBodyAttributeTests
{
    static OpenApiRequestBodyAttributeTests() =>
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [OpenApiSchemaComponent(Title = "User Payload", Description = "Schema for user payload")]
    private class UserPayload
    {
        public string Name { get; set; } = "Alice";
        public int Age { get; set; } = 42;
    }

    [OpenApiExampleComponent(Key = "UserEx", Summary = "User example", Description = "Example user")]
    private class UserExample
    {
        public string Name { get; set; } = "Bob";
    }

    [OpenApiRequestBodyComponent(Key = "UserBody", Description = "User body", ContentType = ["application/json"], IsRequired = true, Example = "AlicePayload")]
    private class RequestBodyHolderBasic
    {
        [OpenApiProperty(Description = "Username", Nullable = false)]
        public string Name { get; set; } = "Alice";
    }

    [OpenApiRequestBodyComponent(Key = "UserBodyWithExamples", Description = "User body with examples", ContentType = ["application/json"], IsRequired = false)]
    [OpenApiExampleRef(Key = "exClone", ReferenceId = "UserEx", ContentType = "application/json", Inline = true)]
    [OpenApiExampleRef(Key = "exRef", ReferenceId = "UserEx", ContentType = "application/json", Inline = false)]
    private class RequestBodyHolderWithExamples
    {
        public string Name { get; set; } = "Bob";
    }

    [Fact]
    public void RequestBodyComponent_BasicFields_AppliedCorrectly()
    {
        var host = new KestrunHost("ReqBodyTest", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument("rb1");
        var set = new OpenApiComponentSet
        {
            SchemaTypes = [typeof(UserPayload)],
            RequestBodyTypes = [typeof(RequestBodyHolderBasic)],
            ExampleTypes = [typeof(UserExample)]
        };

        descriptor.GenerateComponents(set);

        Assert.NotNull(descriptor.Document.Components);
        var bodies = descriptor.Document.Components.RequestBodies;
        Assert.NotNull(bodies);
        Assert.Contains("UserBody", bodies.Keys);

        var body = (Microsoft.OpenApi.OpenApiRequestBody)bodies["UserBody"];
        Assert.Equal("User body", body.Description);
        Assert.True(body.Required);
        Assert.NotNull(body.Content);
        Assert.True(body.Content.TryGetValue("application/json", out var media));
        Assert.NotNull(media.Schema);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(media.Schema);
        Assert.NotNull(media.Example);
    }

    [Fact]
    public void RequestBodyComponent_ExampleRefs_InlineVsReference()
    {
        var host = new KestrunHost("ReqBodyTest2", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument("rb2");
        var set = new OpenApiComponentSet
        {
            SchemaTypes = [typeof(UserPayload)],
            RequestBodyTypes = [typeof(RequestBodyHolderWithExamples)],
            ExampleTypes = [typeof(UserExample)]
        };

        descriptor.GenerateComponents(set);

        Assert.NotNull(descriptor.Document.Components);
        var bodies = descriptor.Document.Components.RequestBodies;
        Assert.NotNull(bodies);
        Assert.Contains("UserBodyWithExamples", bodies.Keys);
        var body = (Microsoft.OpenApi.OpenApiRequestBody)bodies["UserBodyWithExamples"];
        Assert.NotNull(body.Content);
        Assert.True(body.Content.TryGetValue("application/json", out var media));
        Assert.NotNull(media.Schema);
        Assert.NotNull(media.Schema);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(media.Schema);
        Assert.NotNull(media.Examples);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiExample>(media.Examples["exClone"]);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiExampleReference>(media.Examples["exRef"]);
    }
}
