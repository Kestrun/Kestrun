using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorInlineAndPrivateHelpersTests
{
    [Fact]
    public void TryGetInline_ReturnsFalse_WhenInlineComponentsAreNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        Assert.False(d.TryGetInline<OpenApiExample>("missing", OpenApiComponentKind.Examples, out _));
    }

    [Fact]
    public void TryGetComponent_ReturnsFalse_WhenComponentsAreNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        Assert.False(d.TryGetComponent<OpenApiExample>("missing", OpenApiComponentKind.Examples, out _));
    }

    [Fact]
    public void AddInlineExample_InvalidConflictResolution_ThrowsArgumentOutOfRange()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            d.AddInlineExample("ex", new OpenApiExample(), (OpenApiComponentConflictResolution)999));
    }

    [Fact]
    public void TryGetComponent_InvalidKind_ThrowsArgumentOutOfRange()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            d.TryGetComponent<OpenApiExample>("ex", (OpenApiComponentKind)999, out _));
    }

    [Fact]
    public void TryAddExample_AddsClone_WhenExampleIsInline()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddInlineExample("exInline", new OpenApiExample { Summary = "inline" });
        Assert.True(d.TryGetInline<OpenApiExample>("exInline", OpenApiComponentKind.Examples, out var inlineEx));

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "exInline",
            Inline = true
        };

        var added = InvokeTryAddExample(d, examples, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiExample>(examples["k"]);
        Assert.Equal("inline", stored.Summary);
        Assert.NotSame(inlineEx, stored);
    }

    [Fact]
    public void TryAddExample_AddsReference_WhenExampleIsComponent_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddComponentExample("exComp", new OpenApiExample { Summary = "component" });

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "exComp",
            Inline = false
        };

        var added = InvokeTryAddExample(d, examples, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiExampleReference>(examples["k"]);
        Assert.Equal("exComp", stored.Reference.Id);
    }

    [Fact]
    public void TryAddExample_ReturnsFalse_WhenMissing_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = false
        };

        var added = InvokeTryAddExample(d, examples, attr);

        Assert.False(added);
        Assert.False(examples.ContainsKey("k"));
    }

    [Fact]
    public void TryAddExample_Throws_WhenMissing_AndInlineTrue()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = true
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeTryAddExample(d, examples, attr));
    }

    [Fact]
    public void TryAddLink_AddsClone_WhenLinkIsInline()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddInlineLink("lnkInline", new OpenApiLink
        {
            Description = "inline",
            OperationId = "op",
            Parameters = new Dictionary<string, RuntimeExpressionAnyWrapper>
            {
                ["id"] = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.ToNode("abc") }
            },
            RequestBody = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.ToNode(new { x = 1 }) }
        });

        Assert.True(d.TryGetInline<OpenApiLink>("lnkInline", OpenApiComponentKind.Links, out var inlineLnk));

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "lnkInline",
            Inline = true
        };

        var added = InvokeTryAddLink(d, links, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiLink>(links["k"]);
        Assert.Equal("inline", stored.Description);
        Assert.NotSame(inlineLnk, stored);
    }

    [Fact]
    public void TryAddLink_AddsReference_WhenLinkIsComponent_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddComponentLink("lnkComp", new OpenApiLink
        {
            Description = "component",
            OperationId = "op",
            Parameters = new Dictionary<string, RuntimeExpressionAnyWrapper>
            {
                ["id"] = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.ToNode("abc") }
            },
            RequestBody = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.ToNode(new { x = 1 }) }
        });

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "lnkComp",
            Inline = false
        };

        var added = InvokeTryAddLink(d, links, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiLinkReference>(links["k"]);
        Assert.Equal("lnkComp", stored.Reference.Id);
    }

    [Fact]
    public void TryAddLink_ReturnsFalse_WhenMissing_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = false
        };

        var added = InvokeTryAddLink(d, links, attr);

        Assert.False(added);
        Assert.False(links.ContainsKey("k"));
    }

    [Fact]
    public void TryAddLink_Throws_WhenMissing_AndInlineTrue()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = true
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeTryAddLink(d, links, attr));
    }

    [Fact]
    public void ApplyResponseLinkAttribute_Throws_WhenStatusCodeIsNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var meta = new OpenAPIPathMetadata(new MapRouteOptions());
        var attr = new OpenApiResponseLinkRefAttribute
        {
            StatusCode = null!,
            Key = "k",
            ReferenceId = "x"
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeApplyResponseLinkAttribute(d, meta, attr));
    }

    [Fact]
    public void ApplyResponseLinkAttribute_Throws_WhenKeyIsNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var meta = new OpenAPIPathMetadata(new MapRouteOptions());
        var attr = new OpenApiResponseLinkRefAttribute
        {
            StatusCode = "200",
            Key = null!,
            ReferenceId = "x"
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeApplyResponseLinkAttribute(d, meta, attr));
    }

    [Fact]
    public void BuildOperationFromMetadata_AutoAddsSpecificClientErrorResponses_WhenApplicable()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var mapOptions = new MapRouteOptions
        {
            AllowedRequestContentTypes = ["application/json"],
            DefaultResponseContentType = new Dictionary<string, ICollection<ContentTypeWithSchema>>(StringComparer.Ordinal)
            {
                ["200"] = [new ContentTypeWithSchema("application/json")]
            }
        };

        var meta = new OpenAPIPathMetadata(mapOptions)
        {
            Parameters = [new OpenApiParameter { Name = "id", In = ParameterLocation.Query }],
            RequestBody = new OpenApiRequestBody(),
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "Success" }
            }
        };

        var operation = InvokeBuildOperationFromMetadata(d, meta);

        Assert.Contains("400", operation.Responses.Keys);
        Assert.Contains("406", operation.Responses.Keys);
        Assert.Contains("415", operation.Responses.Keys);
        Assert.Contains("422", operation.Responses.Keys);

        var auto422 = Assert.IsType<OpenApiResponse>(operation.Responses["422"]);
        var defaultContent = Assert.IsType<OpenApiMediaType>(auto422.Content?[OpenApiDocDescriptor.DefaultAutoErrorResponseContentType]);
        var schemaRef = Assert.IsType<OpenApiSchemaReference>(defaultContent.Schema);
        Assert.Equal("KestrunErrorResponse", schemaRef.Reference.Id);

        var autoSchema = Assert.IsType<OpenApiSchema>(d.Document.Components?.Schemas?["KestrunErrorResponse"]);
        var requiredFields = Assert.IsAssignableFrom<ISet<string>>(autoSchema.Required);
        Assert.Contains("status", requiredFields);
        Assert.Contains("error", requiredFields);
    }

    [Fact]
    public void BuildOperationFromMetadata_UsesConfiguredAutoErrorContentTypes()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId)
        {
            AutoErrorResponseContentTypes = ["application/problem+json", "application/xml"]
        };

        var meta = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            Parameters = [new OpenApiParameter { Name = "id", In = ParameterLocation.Query }],
            RequestBody = new OpenApiRequestBody(),
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "Success" }
            }
        };

        var operation = InvokeBuildOperationFromMetadata(d, meta);
        var auto400 = Assert.IsType<OpenApiResponse>(operation.Responses["400"]);

        Assert.NotNull(auto400.Content);
        Assert.Contains("application/problem+json", auto400.Content.Keys);
        Assert.Contains("application/xml", auto400.Content.Keys);
        Assert.DoesNotContain("application/json", auto400.Content.Keys);

        var problemJson = Assert.IsType<OpenApiMediaType>(auto400.Content["application/problem+json"]);
        var schemaRef = Assert.IsType<OpenApiSchemaReference>(problemJson.Schema);
        Assert.Equal("KestrunErrorResponse", schemaRef.Reference.Id);
    }

    [Fact]
    public void BuildOperationFromMetadata_DoesNotOverrideExplicitStatusResponse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var mapOptions = new MapRouteOptions
        {
            AllowedRequestContentTypes = ["application/json"]
        };

        var meta = new OpenAPIPathMetadata(mapOptions)
        {
            RequestBody = new OpenApiRequestBody(),
            Responses = new OpenApiResponses
            {
                ["415"] = new OpenApiResponse { Description = "Custom Unsupported Media Type" }
            }
        };

        var operation = InvokeBuildOperationFromMetadata(d, meta);

        var explicit415 = Assert.IsType<OpenApiResponse>(operation.Responses["415"]);
        Assert.Equal("Custom Unsupported Media Type", explicit415.Description);
        Assert.Contains("400", operation.Responses.Keys);
        Assert.Contains("422", operation.Responses.Keys);
    }

    [Fact]
    public void BuildOperationFromMetadata_SkipsAutoClientErrors_WhenRangeOrDefaultExists()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var metaWith4xx = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            Parameters = [new OpenApiParameter { Name = "id", In = ParameterLocation.Query }],
            RequestBody = new OpenApiRequestBody(),
            Responses = new OpenApiResponses
            {
                ["4XX"] = new OpenApiResponse { Description = "Any client error" }
            }
        };

        var opWith4xx = InvokeBuildOperationFromMetadata(d, metaWith4xx);
        Assert.DoesNotContain("400", opWith4xx.Responses.Keys);
        Assert.DoesNotContain("406", opWith4xx.Responses.Keys);
        Assert.DoesNotContain("415", opWith4xx.Responses.Keys);
        Assert.DoesNotContain("422", opWith4xx.Responses.Keys);

        var metaWithDefault = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            RequestBody = new OpenApiRequestBody(),
            Responses = new OpenApiResponses
            {
                ["default"] = new OpenApiResponse { Description = "Default error" }
            }
        };

        var opWithDefault = InvokeBuildOperationFromMetadata(d, metaWithDefault);
        Assert.DoesNotContain("400", opWithDefault.Responses.Keys);
        Assert.DoesNotContain("406", opWithDefault.Responses.Keys);
        Assert.DoesNotContain("415", opWithDefault.Responses.Keys);
        Assert.DoesNotContain("422", opWithDefault.Responses.Keys);
    }

    private static bool InvokeTryAddExample(OpenApiDocDescriptor d, IDictionary<string, IOpenApiExample> examples, IOpenApiExampleAttribute attr)
    {
        var m = typeof(OpenApiDocDescriptor).GetMethod("TryAddExample", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        object?[] args = [examples, attr];
        try
        {
            return (bool)m.Invoke(d, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static bool InvokeTryAddLink(OpenApiDocDescriptor d, IDictionary<string, IOpenApiLink> links, OpenApiResponseLinkRefAttribute attr)
    {
        var m = typeof(OpenApiDocDescriptor).GetMethod("TryAddLink", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        object?[] args = [links, attr];
        try
        {
            return (bool)m.Invoke(d, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static void InvokeApplyResponseLinkAttribute(OpenApiDocDescriptor d, OpenAPIPathMetadata meta, OpenApiResponseLinkRefAttribute attr)
    {
        var m = typeof(OpenApiDocDescriptor).GetMethod("ApplyResponseLinkAttribute", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        object?[] args = [meta, attr];
        try
        {
            _ = m.Invoke(d, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static OpenApiOperation InvokeBuildOperationFromMetadata(OpenApiDocDescriptor d, OpenAPIPathMetadata meta)
    {
        var m = typeof(OpenApiDocDescriptor).GetMethod("BuildOperationFromMetadata", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        object?[] args = [meta];
        try
        {
            return Assert.IsType<OpenApiOperation>(m.Invoke(d, args));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
