using System.Collections;
using System.Text.Json.Nodes;
using System.Management.Automation;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.OpenApi;
using Xunit;

namespace KestrunTests.Languages;

public class ParameterForInjectionInfoTests
{
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
                        metadata: new EndpointMetadataCollection(parameters),
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
        Assert.Throws<ArgumentNullException>(() => new ParameterForInjectionInfo(metadata, (OpenApiParameter?)null!));
        Assert.Throws<ArgumentNullException>(() => new ParameterForInjectionInfo((ParameterMetadata)null!, new OpenApiParameter()));
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_FromOpenApiRequestBody_SetsInNull_AndSchemaProperties()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var schemaDefault = JsonValue.Create("x");

        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
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
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference()
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
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var ctx = CreateContextWithEndpointParameters(
            new List<ParameterForInjectionInfo> { p },
            method: "POST",
            path: "/",
            body: "{\"a\":1,\"b\":\"x\"}",
            configureContext: http => http.Request.ContentType = "application/json");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        Assert.NotNull(ctx.Parameters.Body);
        Assert.True(ctx.Parameters.Body!.Value is Hashtable);
        var ht = (Hashtable)ctx.Parameters.Body.Value!;
        Assert.Equal(1L, ht["a"]);
        Assert.Equal("x", ht["b"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void InjectParameters_RequestBodyYaml_ConvertsToHashtable()
    {
        var metadata = new ParameterMetadata("body", typeof(object));
        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/yaml"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var ctx = CreateContextWithEndpointParameters(
            new List<ParameterForInjectionInfo> { p },
            method: "POST",
            path: "/",
            body: "a: 1\nb: test\n",
            configureContext: http => http.Request.ContentType = "application/yaml");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        Assert.NotNull(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(ctx.Parameters.Body!.Value);
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
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/xml"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };
        var p = new ParameterForInjectionInfo(metadata, requestBody);

        var ctx = CreateContextWithEndpointParameters(
            new List<ParameterForInjectionInfo> { p },
            method: "POST",
            path: "/",
            body: "<root><a>1</a><b>test</b></root>",
            configureContext: http => http.Request.ContentType = "application/xml");

        using var ps = PowerShell.Create();
        _ = ps.AddCommand("Write-Output");

        ParameterForInjectionInfo.InjectParameters(ctx, ps);

        Assert.NotNull(ctx.Parameters.Body);
        var ht = Assert.IsType<Hashtable>(ctx.Parameters.Body!.Value);
        Assert.Equal("1", ht["a"]);
        Assert.Equal("test", ht["b"]);
    }
}
