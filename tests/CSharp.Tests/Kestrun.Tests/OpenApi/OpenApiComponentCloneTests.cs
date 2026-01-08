using System.Text.Json.Nodes;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Moq;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiComponentCloneTests
{
    [Fact]
    public void OpenApiExample_Clone_IsDeepCloneForJson()
    {
        var original = new OpenApiExample
        {
            Summary = "s",
            Value = JsonNode.Parse("{\"a\":1}")
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Summary, clone.Summary);

        var cloneObj = Assert.IsType<JsonObject>(clone.Value);
        cloneObj["a"] = 2;

        var originalObj = Assert.IsType<JsonObject>(original.Value);
        Assert.Equal(1, (int)originalObj["a"]!);
    }

    [Fact]
    public void IOpenApiParameter_Clone_ThrowsForUnsupportedImplementation()
    {
        var mock = new Mock<IOpenApiParameter>();

        _ = Assert.Throws<InvalidOperationException>(() => mock.Object.Clone());
    }

    [Fact]
    public void OpenApiLink_Clone_ClonesNestedRuntimeExpressionWrappers()
    {
        var original = new OpenApiLink
        {
            OperationId = "op",
            Parameters = new Dictionary<string, RuntimeExpressionAnyWrapper>
            {
                ["id"] = new RuntimeExpressionAnyWrapper { Any = JsonNode.Parse("\"abc\"") }
            },
            RequestBody = new RuntimeExpressionAnyWrapper { Any = JsonNode.Parse("{\"x\":1}") }
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Parameters, clone.Parameters);
        Assert.NotSame(original.RequestBody, clone.RequestBody);
    }
}
