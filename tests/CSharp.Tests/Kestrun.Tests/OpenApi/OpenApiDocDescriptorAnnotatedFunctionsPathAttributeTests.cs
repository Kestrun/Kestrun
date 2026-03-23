using System.Management.Automation;
using System.Reflection;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorAnnotatedFunctionsPathAttributeTests
{
    [Fact]
    public void ApplyPathAttribute_Throws_WhenPatternIsEmpty()
    {
        var func = CreateFunctionInfo("function Test-Path { param() }", "Test-Path");
        var routeOptions = new MapRouteOptions();
        var metadata = new OpenAPIPathMetadata(routeOptions);
        var attr = new OpenApiPathAttribute { HttpVerb = "GET", Pattern = "" };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeStatic("ApplyPathAttribute", [func, null, routeOptions, metadata, HttpVerb.Get, attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ApplyPathAttribute_Callback_SetsCallbackSpecificFields()
    {
        var func = CreateFunctionInfo("function Send-Status { param() }", "Send-Status");
        var routeOptions = new MapRouteOptions();
        var metadata = new OpenAPIPathMetadata(routeOptions);

        var attr = new OpenApiCallbackAttribute
        {
            HttpVerb = "POST",
            Pattern = "/callback/status",
            Expression = "{$request.body#/callbackUrls/status}",
            Inline = true,
            OperationId = ""
        };

        _ = (HttpVerb)InvokeStatic("ApplyPathAttribute", [func, null!, routeOptions, metadata, HttpVerb.Get, attr])!;

        Assert.Equal(OpenApiPathLikeKind.Callback, metadata.PathLikeKind);
        Assert.True(metadata.Inline);
        Assert.Equal("Send-Status", metadata.Pattern);
        Assert.NotNull(metadata.Expression);
        Assert.StartsWith("Send-Status__post__", metadata.OperationId, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyPathAttribute_Callback_Throws_WhenExpressionIsMissing()
    {
        var func = CreateFunctionInfo("function Send-Status { param() }", "Send-Status");
        var routeOptions = new MapRouteOptions();
        var metadata = new OpenAPIPathMetadata(routeOptions);

        var attr = new OpenApiCallbackAttribute
        {
            HttpVerb = "POST",
            Pattern = "/callback/status",
            Expression = " "
        };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeStatic("ApplyPathAttribute", [func, null, routeOptions, metadata, HttpVerb.Get, attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ApplyPathAttribute_Webhook_SetsWebhookKindAndOperationIdFallback()
    {
        var func = CreateFunctionInfo("function Notify-Webhook { param() }", "Notify-Webhook");
        var routeOptions = new MapRouteOptions();
        var metadata = new OpenAPIPathMetadata(routeOptions);

        var attr = new OpenApiWebhookAttribute
        {
            HttpVerb = "PUT",
            Pattern = "/webhooks/orders",
            OperationId = null
        };

        var parsed = (HttpVerb)InvokeStatic("ApplyPathAttribute", [func, null!, routeOptions, metadata, HttpVerb.Get, attr])!;

        Assert.Equal(HttpVerb.Put, parsed);
        Assert.Equal(OpenApiPathLikeKind.Webhook, metadata.PathLikeKind);
        Assert.Equal("/webhooks/orders", metadata.Pattern);
        Assert.Equal("Notify-Webhook", metadata.OperationId);
    }

    private static FunctionInfo CreateFunctionInfo(string scriptText, string functionName)
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript(scriptText);
        _ = ps.Invoke();

        return (ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand(functionName, CommandTypes.Function) as FunctionInfo)
            ?? throw new InvalidOperationException($"Function '{functionName}' was not created.");
    }

    private static object? InvokeStatic(string methodName, object?[] args)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 6)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(null, args);
    }
}
