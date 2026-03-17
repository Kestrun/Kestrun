using System.Collections;
using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Microsoft.OpenApi;
using Moq;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorLowCoverageTests
{
    [Fact]
    public void ApplyCallbackRefAttribute_AddsReference_AndCompilesPlan()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal);

        var callback = new OpenApiCallback
        {
            PathItems = new Dictionary<RuntimeExpression, IOpenApiPathItem>
            {
                [RuntimeExpression.Build("{$request.body#/url}")] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = new OpenApiOperation { OperationId = "cb_post_status" }
                    }
                }
            }
        };

        descriptor.Document.Components.Callbacks["StatusCallback"] = callback;

        var map = new MapRouteOptions { Pattern = "/payments/{id}" };
        var metadata = new OpenAPIPathMetadata(map);
        var attr = new OpenApiCallbackRefAttribute
        {
            Key = "status",
            ReferenceId = "StatusCallback",
            Inline = false
        };

        _ = InvokePrivate(descriptor, "ApplyCallbackRefAttribute", [metadata, attr]);

        Assert.NotNull(metadata.Callbacks);
        var stored = Assert.IsType<OpenApiCallbackReference>(metadata.Callbacks["status"]);
        Assert.Equal("StatusCallback", stored.Reference.Id);
        Assert.NotEmpty(metadata.MapOptions.CallbackPlan);
    }

    [Fact]
    public void ApplyCallbackRefAttribute_InlineMissing_Throws()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var metadata = new OpenAPIPathMetadata(new MapRouteOptions());

        var attr = new OpenApiCallbackRefAttribute
        {
            Key = "status",
            ReferenceId = "missing",
            Inline = true
        };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ApplyCallbackRefAttribute", [metadata, attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void BuildCallbacks_BuildsDocumentCallbackPathItems_ForSamePatternDifferentMethods()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var map = new MapRouteOptions { Pattern = "/v1/payments/{paymentId}/status" };

        var m1 = new OpenAPIPathMetadata(map)
        {
            Expression = RuntimeExpression.Build("{$request.body#/callbackUrls/status}"),
            Inline = false,
            OperationId = "cb_status_post",
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "ok" } }
        };

        var m2 = new OpenAPIPathMetadata(map)
        {
            Expression = RuntimeExpression.Build("{$request.body#/callbackUrls/status}"),
            Inline = false,
            OperationId = "cb_status_put",
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "ok" } }
        };

        var dict = new Dictionary<(string Pattern, HttpVerb Method), OpenAPIPathMetadata>
        {
            [("/v1/payments/{paymentId}/status", HttpVerb.Post)] = m1,
            [("/v1/payments/{paymentId}/status", HttpVerb.Put)] = m2
        };

        _ = InvokePrivate(descriptor, "BuildCallbacks", [dict]);

        Assert.NotNull(descriptor.Document.Components);
        Assert.NotNull(descriptor.Document.Components.Callbacks);
        Assert.True(descriptor.Document.Components.Callbacks.ContainsKey("/v1/payments/{paymentId}/status"));
        var callback = Assert.IsType<OpenApiCallback>(descriptor.Document.Components.Callbacks["/v1/payments/{paymentId}/status"]);
        Assert.NotNull(callback.PathItems);
        var onlyPathItem = Assert.Single(callback.PathItems);
        var pathItem = Assert.IsType<OpenApiPathItem>(onlyPathItem.Value);
        Assert.NotNull(pathItem.Operations);
        Assert.Contains(HttpMethod.Post, pathItem.Operations.Keys);
        Assert.Contains(HttpMethod.Put, pathItem.Operations.Keys);
    }

    [Fact]
    public void ProcessCallbackOperation_WithMissingExpression_Throws()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var metadata = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            Expression = null,
            OperationId = "missing_expression"
        };

        var kvp = new KeyValuePair<(string Pattern, HttpVerb Method), OpenAPIPathMetadata>(
            ("/callback", HttpVerb.Post),
            metadata);

        var callbackItem = new OpenApiCallback();

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessCallbackOperation", [kvp, callbackItem]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ProcessCallbackOperation_WithNonOpenApiPathItem_Throws()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var expr = RuntimeExpression.Build("{$request.body#/callbackUrl}");
        var metadata = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            Expression = expr,
            OperationId = "cb_non_path_item"
        };

        var kvp = new KeyValuePair<(string Pattern, HttpVerb Method), OpenAPIPathMetadata>(
            ("/callback", HttpVerb.Post),
            metadata);

        var fakePathItem = new Mock<IOpenApiPathItem>().Object;
        var callbackItem = new OpenApiCallback
        {
            PathItems = new Dictionary<RuntimeExpression, IOpenApiPathItem>
            {
                [expr] = fakePathItem
            }
        };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessCallbackOperation", [kvp, callbackItem]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void NewOpenApiLink_ValidatesAndBuildsFields()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var ex = Assert.Throws<ArgumentException>(() =>
            descriptor.NewOpenApiLink("#/paths/~1x/get", "op1", null, null, null, null, null));
        Assert.Contains("mutually exclusive", ex.Message);

        var parameters = new Hashtable
        {
            ["id"] = "$response.body#/id",
            ["meta"] = new { a = 1 }
        };

        var extensions = new Hashtable
        {
            ["x-trace"] = new { enabled = true },
            ["invalid"] = "ignored",
            ["x-null"] = null
        };

        var link = descriptor.NewOpenApiLink(
            operationRef: null,
            operationId: "getPayment",
            description: "Get created payment",
            server: new OpenApiServer { Url = "https://api.example.com" },
            parameters: parameters,
            requestBody: "$request.body#/source",
            extensions: extensions);

        Assert.Equal("getPayment", link.OperationId);
        Assert.Equal("Get created payment", link.Description);
        Assert.NotNull(link.Server);
        Assert.NotNull(link.Parameters);
        Assert.Equal(2, link.Parameters.Count);
        Assert.NotNull(link.RequestBody);
        Assert.NotNull(link.RequestBody.Expression);
        Assert.Equal("$request.body#/source", link.RequestBody.Expression.Expression);
        Assert.NotNull(link.Extensions);
        Assert.True(link.Extensions.ContainsKey("x-trace"));
        Assert.False(link.Extensions.ContainsKey("invalid"));
        Assert.False(link.Extensions.ContainsKey("x-null"));
    }

    [Fact]
    public void RequestBodyHelpers_ProcessAndLookup_Work()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var variable = new OpenApiComponentAnnotationScanner.AnnotatedVariable("CreateBody")
        {
            VariableType = typeof(string),
            InitialValue = "abc",
            NoDefault = false
        };

        var attr = new OpenApiRequestBodyComponentAttribute
        {
            Description = "create payload",
            ContentType = ["application/json", "application/xml"],
            Required = true,
            Inline = false
        };

        _ = InvokePrivate(descriptor, "ProcessRequestBodyComponent", [variable, attr]);

        var tuple = InvokePrivateWithOuts(descriptor, "TryGetRequestBodyItem", ["CreateBody", null, null]);
        Assert.True((bool)tuple!.Result!);
        var body = Assert.IsType<OpenApiRequestBody>(tuple.Args[1]!);
        var isInline = Assert.IsType<bool>(tuple.Args[2]!);
        Assert.False(isInline);
        Assert.Equal("create payload", body.Description);
        Assert.True(body.Required);
        Assert.NotNull(body.Content);
        Assert.Contains("application/json", body.Content!.Keys);
        Assert.Contains("application/xml", body.Content.Keys);

        var foundSimple = InvokePrivateWithOuts(descriptor, "TryGetRequestBodyItem", ["CreateBody", null]);
        Assert.True((bool)foundSimple!.Result!);
        _ = Assert.IsType<OpenApiRequestBody>(foundSimple.Args[1]!);
    }

    [Fact]
    public void ResponseHelpers_ProcessAndValidationBranches_Work()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var variable = new OpenApiComponentAnnotationScanner.AnnotatedVariable("NotFound")
        {
            VariableType = typeof(string),
            InitialValue = "n/a",
            NoDefault = true
        };

        var descriptorAttr = new OpenApiResponseComponentAttribute
        {
            Description = "not found",
            ContentType = ["application/json"],
            Inline = false
        };

        _ = InvokePrivate(descriptor, "ProcessResponseComponent", [variable, descriptorAttr]);

        var getResponseResult = InvokePrivateWithOuts(descriptor, "TryGetResponseItem", ["NotFound", null, null]);
        Assert.True((bool)getResponseResult!.Result!);
        var resp = Assert.IsType<OpenApiResponse>(getResponseResult.Args[1]!);
        Assert.False((bool)getResponseResult.Args[2]!);
        Assert.Equal("not found", resp.Description);

        var badStatusExample = new OpenApiResponseExampleRefAttribute
        {
            StatusCode = "200",
            Key = "default",
            ReferenceId = "Ex1"
        };

        var ex1 = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessResponseExampleRef", ["NotFound", badStatusExample]));
        _ = Assert.IsType<InvalidOperationException>(ex1.InnerException);

        var badStatusLink = new OpenApiResponseLinkRefAttribute
        {
            StatusCode = "200",
            Key = "next",
            ReferenceId = "L1"
        };

        var ex2 = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessResponseLinkRef", ["NotFound", badStatusLink]));
        _ = Assert.IsType<InvalidOperationException>(ex2.InnerException);

        var badStatusHeader = new OpenApiResponseHeaderRefAttribute
        {
            StatusCode = "200",
            Key = "x-id",
            ReferenceId = "H1"
        };

        var ex3 = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessResponseHeaderRef", ["NotFound", badStatusHeader]));
        _ = Assert.IsType<InvalidOperationException>(ex3.InnerException);
    }

    [Fact]
    public void DescriptorHelper_Getters_And_ExistenceChecks_Work()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        descriptor.Document.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);
        descriptor.Document.Components.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
        descriptor.Document.Components.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        descriptor.Document.Components.Responses ??= new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);
        descriptor.Document.Components.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        descriptor.Document.Components.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal);
        descriptor.Document.Components.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        descriptor.Document.Components.PathItems ??= new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal);

        descriptor.Document.Components.Schemas["S1"] = new OpenApiSchema { Type = JsonSchemaType.String };
        descriptor.Document.Components.Parameters["P1"] = new OpenApiParameter { Name = "p", In = ParameterLocation.Query };
        descriptor.Document.Components.RequestBodies["RB1"] = new OpenApiRequestBody();
        descriptor.Document.Components.Headers["H1"] = new OpenApiHeader();
        descriptor.Document.Components.Responses["R1"] = new OpenApiResponse();
        descriptor.Document.Components.Examples["E1"] = new OpenApiExample();
        descriptor.Document.Components.Callbacks["C1"] = new OpenApiCallback();
        descriptor.Document.Components.Links["L1"] = new OpenApiLink { OperationId = "op" };
        descriptor.Document.Components.PathItems["/x"] = new OpenApiPathItem();

        _ = Assert.IsAssignableFrom<IOpenApiSchema>(InvokePrivate(descriptor, "GetSchema", ["S1"])!);
        _ = Assert.IsType<OpenApiParameter>(InvokePrivate(descriptor, "GetParameter", ["P1"])!);
        _ = Assert.IsType<OpenApiRequestBody>(InvokePrivate(descriptor, "GetRequestBody", ["RB1"])!);
        _ = Assert.IsType<OpenApiHeader>(InvokePrivate(descriptor, "GetHeader", ["H1"])!);
        _ = Assert.IsType<OpenApiResponse>(InvokePrivate(descriptor, "GetResponse", ["R1"])!);

        Assert.True((bool)InvokePrivate(descriptor, "ComponentSchemasExists", ["S1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentRequestBodiesExists", ["RB1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentResponsesExists", ["R1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentParametersExists", ["P1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentExamplesExists", ["E1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentHeadersExists", ["H1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentCallbacksExists", ["C1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentLinksExists", ["L1"])!);
        Assert.True((bool)InvokePrivate(descriptor, "ComponentPathItemsExists", ["/x"])!);
    }

    private static object? InvokePrivate(object target, string name, object?[] args)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method not found: {name}");

        return method.Invoke(target, args);
    }

    private static (object? Result, object?[] Args) InvokePrivateWithOuts(object target, string name, object?[] args)
    {
        var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length)
            ?? throw new InvalidOperationException($"Method overload not found: {name}/{args.Length}");

        var result = method.Invoke(target, args);
        return (result, args);
    }
}
