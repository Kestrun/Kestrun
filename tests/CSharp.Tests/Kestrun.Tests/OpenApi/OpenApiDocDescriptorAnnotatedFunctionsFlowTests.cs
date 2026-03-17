using System.Management.Automation;
using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorAnnotatedFunctionsFlowTests
{
    [Fact]
    public void FinalizeRouteOptions_Path_AddsRouteMetadata()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument(OpenApiDocDescriptor.DefaultDocumentationId);

        var func = CreateFunctionInfo("function Test-Route { param([string]$id) }", "Test-Route");
        var sb = func.ScriptBlock;
        Assert.NotNull(sb);

        var routeOptions = new MapRouteOptions
        {
            ScriptCode = new LanguageOptions { ScriptBlock = sb }
        };

        var metadata = new OpenAPIPathMetadata(routeOptions)
        {
            PathLikeKind = OpenApiPathLikeKind.Path,
            Pattern = "/api/test/{id}",
            DocumentId = [OpenApiDocDescriptor.DefaultDocumentationId]
        };

        _ = InvokeInstance(
            descriptor,
            "FinalizeRouteOptions",
            [func, sb, metadata, routeOptions, HttpVerb.Get]);

        Assert.True(routeOptions.IsOpenApiAnnotatedFunctionRoute);
        Assert.True(routeOptions.OpenAPI.ContainsKey(HttpVerb.Get));
        Assert.Equal("/Test-Route", routeOptions.Pattern);
    }

    [Fact]
    public void FinalizeRouteOptions_Callback_RegistersCallback()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument(OpenApiDocDescriptor.DefaultDocumentationId);

        var func = CreateFunctionInfo("function Test-Callback { param([string]$Body) }", "Test-Callback");
        var sb = func.ScriptBlock;
        Assert.NotNull(sb);

        var routeOptions = new MapRouteOptions { ScriptCode = new LanguageOptions { ScriptBlock = sb } };
        var metadata = new OpenAPIPathMetadata(routeOptions)
        {
            PathLikeKind = OpenApiPathLikeKind.Callback,
            Pattern = "/callbacks/status",
            DocumentId = [OpenApiDocDescriptor.DefaultDocumentationId],
            Expression = RuntimeExpression.Build("{$request.body#/callbackUrls/status}")
        };

        _ = InvokeInstance(
            descriptor,
            "FinalizeRouteOptions",
            [func, sb, metadata, routeOptions, HttpVerb.Post]);

        Assert.True(descriptor.Callbacks.ContainsKey(("/callbacks/status", HttpVerb.Post)));
    }

    [Fact]
    public void FinalizeRouteOptions_Webhook_RegistersWebhook()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument(OpenApiDocDescriptor.DefaultDocumentationId);

        var func = CreateFunctionInfo("function Test-Webhook { param([string]$Body) }", "Test-Webhook");
        var sb = func.ScriptBlock;
        Assert.NotNull(sb);

        var routeOptions = new MapRouteOptions { ScriptCode = new LanguageOptions { ScriptBlock = sb } };
        var metadata = new OpenAPIPathMetadata(routeOptions)
        {
            PathLikeKind = OpenApiPathLikeKind.Webhook,
            Pattern = "/webhooks/events",
            DocumentId = [OpenApiDocDescriptor.DefaultDocumentationId]
        };

        _ = InvokeInstance(
            descriptor,
            "FinalizeRouteOptions",
            [func, sb, metadata, routeOptions, HttpVerb.Post]);

        Assert.True(descriptor.WebHook.ContainsKey(("/webhooks/events", HttpVerb.Post)));
    }

    [Fact]
    public void GetDocDescriptorOrThrow_ThrowsForMissingDocumentId()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = host.GetOrCreateOpenApiDocument(OpenApiDocDescriptor.DefaultDocumentationId);

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeInstance(descriptor, "GetDocDescriptorOrThrow", ["missing-doc", "OpenApiCallback"]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void EnsureParamOnlyScriptBlock_ThrowsForExecutableStatements()
    {
        var func = CreateFunctionInfo("function Invalid-Callback { param([string]$x) Write-Host 'bad' }", "Invalid-Callback");
        var sb = func.ScriptBlock;
        Assert.NotNull(sb);

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeStatic(
                "EnsureParamOnlyScriptBlock",
                [typeof(FunctionInfo), typeof(ScriptBlock), typeof(string)],
                [func, sb, "callback"]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void CreateRequestBodyFromAttribute_CreatesContentAndExample()
    {
        var requestBody = new OpenApiRequestBody();
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };

        var attr = new OpenApiRequestBodyAttribute
        {
            Description = "payload",
            Required = true,
            ContentType = ["application/json"],
            Example = new { id = 1 }
        };

        var created = (bool)InvokeStatic(
            "CreateRequestBodyFromAttribute",
            [typeof(KestrunAnnotation), typeof(OpenApiRequestBody), typeof(IOpenApiSchema)],
            [attr, requestBody, schema])!;

        Assert.True(created);
        Assert.Equal("payload", requestBody.Description);
        Assert.True(requestBody.Required);
        Assert.NotNull(requestBody.Content);
        var media = Assert.IsType<OpenApiMediaType>(requestBody.Content!["application/json"]);
        Assert.NotNull(media.Schema);
        Assert.NotNull(media.Example);
    }

    [Fact]
    public void CreateRequestBodyFromAttribute_ReturnsFalseForUnsupportedAnnotation()
    {
        var requestBody = new OpenApiRequestBody();
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var unsupported = new OpenApiPathAttribute();

        var created = (bool)InvokeStatic(
            "CreateRequestBodyFromAttribute",
            [typeof(KestrunAnnotation), typeof(OpenApiRequestBody), typeof(IOpenApiSchema)],
            [unsupported, requestBody, schema])!;

        Assert.False(created);
        Assert.Null(requestBody.Content);
    }

    private static FunctionInfo CreateFunctionInfo(string scriptText, string functionName)
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript(scriptText);
        _ = ps.Invoke();
        return (ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand(functionName, CommandTypes.Function) as FunctionInfo)
            ?? throw new InvalidOperationException($"Function '{functionName}' was not created.");
    }

    private static object? InvokeInstance(object instance, string methodName, object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(instance, args);
    }

    private static object? InvokeStatic(string methodName, Type[] parameterTypes, object?[] args)
    {
        var method = typeof(OpenApiDocDescriptor).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(null, args);
    }
}
