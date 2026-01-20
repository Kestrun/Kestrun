using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Management.Automation;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.OpenApi;
using MongoDB.Bson;
using PeterO.Cbor;
using Xunit;

namespace KestrunTests.Languages;

public class ParameterForInjectionInfoTests
{
    private sealed class ProductXmlModel
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public decimal Price { get; set; }

        public string[]? Item { get; set; }

        public static Hashtable XmlMetadata => new(StringComparer.OrdinalIgnoreCase)
        {
            ["ClassName"] = "Product",
            ["ClassXml"] = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = "Product",
            },
            ["Properties"] = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = new Hashtable(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = "id",
                    ["Attribute"] = true,
                },
                ["Price"] = new Hashtable(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = "Price",
                    ["Namespace"] = "http://example.com/pricing",
                    ["Prefix"] = "price",
                },
                ["Item"] = new Hashtable(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = "Item",
                    ["Wrapped"] = true,
                },
            },
        };
    }

    private static KestrunContext CreateContextWithEndpointParameters(
        List<ParameterForInjectionInfo> parameters,
        string method = "GET",
        string path = "/",
        Dictionary<string, string>? headers = null,
        string body = "",
        Dictionary<string, string>? form = null,
        Action<DefaultHttpContext>? configureContext = null)
    {
        return TestRequestFactory.CreateContext(
            method: method,
            path: path,
            headers: headers,
            body: body,
            form: form,
            configureContext: http =>
            {
                configureContext?.Invoke(http);
                http.SetEndpoint(
                    new RouteEndpoint(
                        _ => Task.CompletedTask,
                        RoutePatternFactory.Parse(path),
                        order: 0,
                        metadata: new EndpointMetadataCollection((object)parameters),
                        displayName: "TestEndpoint"));
            });
    }

    private static ParameterForInjectionInfo CreateParameter(
        string name,
        Type parameterType,
        ParameterLocation location,
        JsonSchemaType schemaType,
        JsonNode? defaultValue = null)
    {
        var metadata = new ParameterMetadata(name, parameterType);
        var parameter = new OpenApiParameter
        {
            In = location,
            Schema = new OpenApiSchema
            {
                Type = schemaType,
                Default = defaultValue
            }
        };

        return new ParameterForInjectionInfo(metadata, parameter);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_FromOpenApiParameter_SetsProperties()
    {
        var metadata = new ParameterMetadata("id", typeof(int));
        var schemaDefault = JsonValue.Create(123);
        var parameter = new OpenApiParameter
        {
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Default = schemaDefault
            }
        };

        var sut = new ParameterForInjectionInfo(metadata, parameter);

        Assert.Equal("id", sut.Name);
        Assert.Equal(typeof(int), sut.ParameterType);
        Assert.Equal(JsonSchemaType.Integer, sut.Type);
        Assert.Same(schemaDefault, sut.DefaultValue);
        Assert.Equal(ParameterLocation.Query, sut.In);
        Assert.False(sut.IsRequestBody);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_FromOpenApiParameter_ThrowsOnNulls()
    {
        var metadata = new ParameterMetadata("id", typeof(int));
        _ = Assert.Throws<ArgumentNullException>(() => new ParameterForInjectionInfo(metadata, (OpenApiParameter?)null!));
        _ = Assert.Throws<ArgumentNullException>(() => new ParameterForInjectionInfo(null!, new OpenApiParameter()));
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_FromOpenApiRequestBody_SetsInNull_AndSchemaProperties()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var schemaDefault = JsonValue.Create("x");

        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Default = schemaDefault
                    }
                }
            }
        };

        var sut = new ParameterForInjectionInfo(metadata, requestBody);

        Assert.Equal("body", sut.Name);
        Assert.Equal(typeof(object), sut.ParameterType);
        Assert.Equal(JsonSchemaType.Object, sut.Type);
        Assert.Same(schemaDefault, sut.DefaultValue);
        Assert.Null(sut.In);
        Assert.True(sut.IsRequestBody);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_FromOpenApiRequestBody_WithSchemaReference_ForcesObjectType()
    {
        var metadata = new ParameterMetadata("body", typeof(object));

        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference("TestSchema")
                }
            }
        };

        var sut = new ParameterForInjectionInfo(metadata, requestBody);

        Assert.Equal(JsonSchemaType.Object, sut.Type);
        Assert.Null(sut.In);
        Assert.True(sut.IsRequestBody);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_QueryInteger_ConvertsAndStores()
    {
        var p = CreateParameter("age", typeof(int), ParameterLocation.Query, JsonSchemaType.Integer);
        var parameters = new List<ParameterForInjectionInfo> { p };

        var ctx = CreateContextWithEndpointParameters(
            parameters,
            path: "/",
            configureContext: http => http.Request.QueryString = new QueryString("?age=42"));

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        Assert.True(ctx.Parameters.Parameters.TryGetValue("age", out var resolved));
        Assert.Equal(42, resolved.Value);

        var cmd = Assert.Single(ps.Commands.Commands);
        var cmdParam = Assert.Single(cmd.Parameters, x => x.Name == "age");
        Assert.Equal(42, cmdParam.Value);
        Assert.Null(ctx.Parameters.Body);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_WhenValueMissing_UsesDefault()
    {
        var p = CreateParameter(
            name: "limit",
            parameterType: typeof(int),
            location: ParameterLocation.Query,
            schemaType: JsonSchemaType.Integer,
            defaultValue: JsonValue.Create(7));

        var parameters = new List<ParameterForInjectionInfo> { p };
        var ctx = CreateContextWithEndpointParameters(parameters, path: "/");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        Assert.True(ctx.Parameters.Parameters.TryGetValue("limit", out var resolved));
        Assert.Equal(7, resolved.Value);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyJson_ConvertsToHashtable_AndSetsBody()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var jsonBody = JsonSerializer.Serialize(new { a = 1, b = "x" });
        var ctx = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: jsonBody,
            configureContext: http => http.Request.ContentType = "application/json");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(bodyResolved.Value);
        Assert.Equal(1L, Convert.ToInt64(ht["a"]));
        Assert.Equal("x", ht["b"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyYaml_ConvertsToHashtable()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/yaml"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var ctx = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: "a: 1\nb: test\n",
            configureContext: http => http.Request.ContentType = "application/yaml");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(bodyResolved.Value);
        Assert.Equal(1, Convert.ToInt32(ht["a"]));
        Assert.Equal("test", ht["b"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyXml_ConvertsToHashtable()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/xml"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var ctx = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: "<root><a>1</a><b>test</b></root>",
            configureContext: http => http.Request.ContentType = "application/xml");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(bodyResolved.Value);
        Assert.Equal("1", ht["a"]);
        Assert.Equal("test", ht["b"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyXml_WithXmlMetadata_ConvertsToTypedModel()
    {
        var metadata = new ParameterMetadata("body", typeof(ProductXmlModel));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/xml"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var xmlBody = """
<Product id=\"22\" xmlns:price=\"http://example.com/pricing\">
  <Name>My Product</Name>
  <price:Price>11.95</price:Price>
  <Item>
    <Item>Item1</Item>
    <Item>Item2</Item>
  </Item>
</Product>
""";

        var ctx = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: xmlBody,
            configureContext: http => http.Request.ContentType = "application/xml");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctx.Parameters.Body);
        var model = Assert.IsType<ProductXmlModel>(bodyResolved.Value);

        Assert.Equal(22, model.Id);
        Assert.Equal("My Product", model.Name);
        Assert.Equal(11.95m, model.Price);
        Assert.Equal(new[] { "Item1", "Item2" }, model.Item);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyJson_WhenContentTypeMissing_InferFromOpenApiContentType()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var jsonBody = JsonSerializer.Serialize(new { a = 1, b = "x" });
        var ctx = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: jsonBody);

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(bodyResolved.Value);
        Assert.Equal(1L, Convert.ToInt64(ht["a"]));
        Assert.Equal("x", ht["b"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyBson_Base64_ConvertsToHashtable()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/bson"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var doc = new BsonDocument { { "a", 1 }, { "b", "x" } };
        var bytes = doc.ToBson();
        var base64Body = "base64:" + Convert.ToBase64String(bytes);

        var ctx = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: base64Body,
            configureContext: http => http.Request.ContentType = "application/bson");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(bodyResolved.Value);
        Assert.Equal(1, Convert.ToInt32(ht["a"]));
        Assert.Equal("x", ht["b"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyCbor_Base64_ConvertsToHashtable()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/cbor"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var payload = new Dictionary<string, object?> { ["a"] = 1, ["b"] = "x" };
        var bytes = CBORObject.FromObject(payload).EncodeToBytes();
        var base64Body = "base64:" + Convert.ToBase64String(bytes);

        var ctx = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: base64Body,
            configureContext: http => http.Request.ContentType = "application/cbor");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(bodyResolved.Value);
        Assert.Equal(1, Convert.ToInt32(ht["a"]));
        Assert.Equal("x", ht["b"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyCsv_ConvertsToHashtableOrArray()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["text/csv"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var ctxSingle = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: "a,b\n1,x\n",
            configureContext: http => http.Request.ContentType = "text/csv");

        using (var ps = PowerShell.Create())
        {
            _ = ps.AddCommand("Write-Output");
            ParameterForInjectionInfo.InjectParameters(ctxSingle, ps);
        }

        var bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctxSingle.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(bodyResolved.Value);
        Assert.Equal("1", ht["a"]);
        Assert.Equal("x", ht["b"]);

        var ctxMulti = CreateContextWithEndpointParameters(
            [p],
            method: "POST",
            path: "/",
            body: "a,b\n1,x\n2,y\n",
            configureContext: http => http.Request.ContentType = "text/csv");

        using (var ps = PowerShell.Create())
        {
            _ = ps.AddCommand("Write-Output");
            ParameterForInjectionInfo.InjectParameters(ctxMulti, ps);
        }

        bodyResolved = Assert.IsType<ParameterForInjectionResolved>(ctxMulti.Parameters.Body);
        var arr = Assert.IsType<object?[]>(bodyResolved.Value);
        Assert.Equal(2, arr.Length);
        var row0 = Assert.IsType<Hashtable>(arr[0]);
        var row1 = Assert.IsType<Hashtable>(arr[1]);
        Assert.Equal("1", row0["a"]);
        Assert.Equal("x", row0["b"]);
        Assert.Equal("2", row1["a"]);
        Assert.Equal("y", row1["b"]);
    }
}
