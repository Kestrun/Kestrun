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

        _ = Assert.Throws<InvalidOperationException>(mock.Object.Clone);
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

    [Fact]
    public void OpenApiParameter_Clone_ClonesSchemaExamplesContentAndExampleJson()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };

        var mt = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };

        var original = new OpenApiParameter
        {
            Name = "p",
            In = ParameterLocation.Query,
            Required = true,
            Schema = schema,
            Example = JsonNode.Parse("{\"x\":1}")!,
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/json"] = mt
            },
            Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal)
            {
                ["ex"] = new OpenApiExample { Summary = "s", Value = JsonNode.Parse("\"v\"") }
            }
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Schema, clone.Schema);
        Assert.NotSame(original.Content, clone.Content);
        Assert.NotSame(original.Examples, clone.Examples);

        var originalExampleObj = Assert.IsType<JsonObject>(original.Example);
        var cloneExampleObj = Assert.IsType<JsonObject>(clone.Example);
        cloneExampleObj["x"] = 2;
        Assert.Equal(1, (int)originalExampleObj["x"]!);
    }

    [Fact]
    public void OpenApiRequestBody_Clone_ClonesContent()
    {
        var original = new OpenApiRequestBody
        {
            Description = "d",
            Required = true,
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                }
            }
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Content, clone.Content);
        Assert.NotSame(original.Content!["application/json"], clone.Content!["application/json"]);
    }

    [Fact]
    public void OpenApiRequestBody_ConvertToSchema_CopiesDescriptionAndFirstMediaTypeProperties()
    {
        var original = new OpenApiRequestBody
        {
            Description = "body",
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    }
                }
            }
        };

        var schema = original.ConvertToSchema();

        Assert.Equal("body", schema.Description);
        Assert.NotNull(schema.Properties);
        Assert.True(schema.Properties.ContainsKey("name"));
    }

    [Fact]
    public void OpenApiHeader_Clone_ClonesExampleJson()
    {
        var original = new OpenApiHeader
        {
            Description = "h",
            Example = JsonNode.Parse("{\"a\":1}"),
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Schema, clone.Schema);

        var cloneObj = Assert.IsType<JsonObject>(clone.Example);
        cloneObj["a"] = 2;
        var originalObj = Assert.IsType<JsonObject>(original.Example);
        Assert.Equal(1, (int)originalObj["a"]!);
    }

    [Fact]
    public void OpenApiResponse_Clone_ClonesHeadersContentAndLinks()
    {
        var original = new OpenApiResponse
        {
            Description = "resp",
            Headers = new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal)
            {
                ["X-Test"] = new OpenApiHeader { Schema = new OpenApiSchema { Type = JsonSchemaType.String } }
            },
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } }
            },
            Links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal)
            {
                ["lnk"] = new OpenApiLink
                {
                    OperationId = "op",
                    Parameters = new Dictionary<string, RuntimeExpressionAnyWrapper>
                    {
                        ["id"] = new RuntimeExpressionAnyWrapper { Any = JsonNode.Parse("\"abc\"") }
                    },
                    RequestBody = new RuntimeExpressionAnyWrapper { Any = JsonNode.Parse("{\"x\":1}") }
                }
            }
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Headers, clone.Headers);
        Assert.NotSame(original.Content, clone.Content);
        Assert.NotSame(original.Links, clone.Links);
    }

    [Fact]
    public void OpenApiSchema_Clone_ClonesCollectionsAndJsonNodes()
    {
        var original = new OpenApiSchema
        {
            Title = "t",
            Type = JsonSchemaType.Object,
            Default = JsonNode.Parse("{\"x\":1}"),
            Required = new HashSet<string>(StringComparer.Ordinal) { "x" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["x"] = new OpenApiSchema { Type = JsonSchemaType.Integer }
            }
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.Properties, clone.Properties);
        Assert.NotSame(original.Required, clone.Required);

        var cloneObj = Assert.IsType<JsonObject>(clone.Default);
        cloneObj["x"] = 2;
        var originalObj = Assert.IsType<JsonObject>(original.Default);
        Assert.Equal(1, (int)originalObj["x"]!);
    }

    [Fact]
    public void OpenApiCallback_Clone_ClonesPathItemsDictionaryInstance()
    {
        var pathItem = new OpenApiPathItem();
        var original = new OpenApiCallback
        {
            PathItems = new Dictionary<RuntimeExpression, IOpenApiPathItem>
            {
                [RuntimeExpression.Build("{$request.body#/url}")] = pathItem
            }
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.NotSame(original.PathItems, clone.PathItems);
        Assert.Equal(original.PathItems!.Count, clone.PathItems!.Count);
    }

    [Fact]
    public void IOpenApiMediaType_Clone_ClonesOpenApiMediaType()
    {
        IOpenApiMediaType original = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
    }

    [Fact]
    public void IOpenApiExtension_Clone_ThrowsForUnsupportedImplementation()
    {
        var mock = new Mock<IOpenApiExtension>();
        _ = Assert.Throws<InvalidOperationException>(mock.Object.Clone);
    }
}
