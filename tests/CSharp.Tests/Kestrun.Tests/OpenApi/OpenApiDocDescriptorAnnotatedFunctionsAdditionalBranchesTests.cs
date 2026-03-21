using System.Management.Automation;
using System.Reflection;
using Kestrun.Forms;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorAnnotatedFunctionsAdditionalBranchesTests
{
    [Fact]
    public void ApplyFormBindingAttribute_ThrowsWhenTemplateMissing()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var routeOptions = new MapRouteOptions();
        var attr = new KrBindFormAttribute { Template = "missing-template" };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeInstance(descriptor, "ApplyFormBindingAttribute", [routeOptions, attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ApplyFormBindingAttribute_ClonesTemplateOptions()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var routeOptions = new MapRouteOptions();

        var template = new KrFormOptions { Name = "upload" };
        template.AllowedContentTypes.Add("multipart/form-data");
        host.Runtime.FormOptions["upload"] = template;

        var attr = new KrBindFormAttribute { Template = "upload" };
        _ = InvokeInstance(descriptor, "ApplyFormBindingAttribute", [routeOptions, attr]);

        Assert.NotNull(routeOptions.FormOptions);
        Assert.NotSame(template, routeOptions.FormOptions);
        Assert.Equal("upload", routeOptions.FormOptions.Name);
        Assert.Contains("multipart/form-data", routeOptions.FormOptions.AllowedContentTypes);
    }

    [Fact]
    public void ApplyExtensionAttribute_IgnoresNullJsonNode()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var metadata = new OpenAPIPathMetadata(new MapRouteOptions());

        var attr = new OpenApiExtensionAttribute { Name = "x-null", Json = "null" };
        _ = InvokeInstance(descriptor, "ApplyExtensionAttribute", [metadata, attr]);

        Assert.NotNull(metadata.Extensions);
        Assert.False(metadata.Extensions!.ContainsKey("x-null"));
    }

    [Fact]
    public void ApplyExtensionAttribute_AddsJsonNodeExtension()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var metadata = new OpenAPIPathMetadata(new MapRouteOptions());

        var attr = new OpenApiExtensionAttribute { Name = "x-meta", Json = /*lang=json,strict*/ "{\"a\":1}" };
        _ = InvokeInstance(descriptor, "ApplyExtensionAttribute", [metadata, attr]);

        Assert.NotNull(metadata.Extensions);
        Assert.True(metadata.Extensions!.ContainsKey("x-meta"));
    }

    [Fact]
    public void IsMultipartContentType_ReturnsExpectedValues()
    {
        Assert.True((bool)InvokeStatic("IsMultipartContentType", ["multipart/form-data"])!);
        Assert.False((bool)InvokeStatic("IsMultipartContentType", ["application/json"])!);
    }

    [Fact]
    public void IsProbablyFileRule_CoversDecisionBranches()
    {
        var byDisk = new KrFormPartRule { Name = "f1", StoreToDisk = true };
        var byExt = new KrFormPartRule { Name = "f2", StoreToDisk = false };
        byExt.AllowedExtensions.Add(".png");
        var byContentType = new KrFormPartRule { Name = "f3", StoreToDisk = false };
        byContentType.AllowedContentTypes.Add("image/png");
        var textOnly = new KrFormPartRule { Name = "txt", StoreToDisk = false };
        textOnly.AllowedContentTypes.Add("text/plain");

        Assert.True((bool)InvokeStatic("IsProbablyFileRule", [byDisk])!);
        Assert.True((bool)InvokeStatic("IsProbablyFileRule", [byExt])!);
        Assert.True((bool)InvokeStatic("IsProbablyFileRule", [byContentType])!);
        Assert.False((bool)InvokeStatic("IsProbablyFileRule", [textOnly])!);
    }

    [Fact]
    public void ApplyRequestBodyExampleRefAttribute_ThrowsWhenRequestBodyMissing()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var metadata = new OpenAPIPathMetadata(new MapRouteOptions());
        var attr = new OpenApiRequestBodyExampleRefAttribute { Key = "k1", ReferenceId = "e1" };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeInstance(descriptor, "ApplyRequestBodyExampleRefAttribute", [metadata, attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ApplyRequestBodyExampleRefAttribute_ThrowsWhenRequestBodyHasNoContent()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var metadata = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            RequestBody = new OpenApiRequestBody { Description = "x" }
        };
        var attr = new OpenApiRequestBodyExampleRefAttribute { Key = "k1", ReferenceId = "e1" };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeInstance(descriptor, "ApplyRequestBodyExampleRefAttribute", [metadata, attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void TryGetFirstRequestBodySchema_ReturnsFalseWhenComponentMissing()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var (Result, Args) = InvokePrivateWithOuts(descriptor, "TryGetFirstRequestBodySchema", ["missing", null]);

        Assert.False((bool)Result!);
    }

    [Fact]
    public void IsRequestBodySchemaMatchForParameter_ReturnsFalseForMismatchedSchemaReference()
    {
        var schema = new OpenApiSchemaReference("OrderBody", null!);

        var matched = (bool)InvokeStatic("IsRequestBodySchemaMatchForParameter", [schema, typeof(int)])!;

        Assert.False(matched);
    }

    [Fact]
    public void FindReferenceIdForParameter_ThrowsWhenSchemaDoesNotMatch()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>();
        descriptor.Document.Components.RequestBodies["BodyA"] = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                }
            }
        };

        var func = CreateFunctionInfo("function Test-Body { param([int]$Body) }", "Test-Body");
        var bodyParam = func.Parameters["Body"];

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeInstance(descriptor, "FindReferenceIdForParameter", ["BodyA", bodyParam]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
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
            .FirstOrDefault(m => m.Name == methodName)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(null, args);
    }

    private static object? InvokeInstance(object instance, string methodName, object?[] args)
    {
        var method = instance.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == methodName)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(instance, args);
    }

    private static (object? Result, object?[] Args) InvokePrivateWithOuts(object instance, string methodName, object?[] args)
    {
        var method = instance.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        var result = method.Invoke(instance, args);
        return (result, args);
    }
}
