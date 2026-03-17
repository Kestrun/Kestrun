using System.Management.Automation;
using System.Reflection;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorRequestBodyRefResolutionTests
{
    [Fact]
    public void ResolveRequestBodyReferenceId_ThrowsForObjectWithoutReferenceId()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var func = CreateFunctionInfo("function Test-Body { param([object]$Body) }", "Test-Body");
        var bodyParam = func.Parameters["Body"];
        var attr = new OpenApiRequestBodyRefAttribute { StatusCode = "200", ReferenceId = " " };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeInstance(descriptor, "ResolveRequestBodyReferenceId", [attr, bodyParam]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ResolveRequestBodyReferenceId_InfersTypeNameForTypedParameter()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var func = CreateFunctionInfo("function Test-Body { param([int]$Body) }", "Test-Body");
        var bodyParam = func.Parameters["Body"];
        var attr = new OpenApiRequestBodyRefAttribute { StatusCode = "200", ReferenceId = "" };

        var result = (string)InvokeInstance(descriptor, "ResolveRequestBodyReferenceId", [attr, bodyParam])!;

        Assert.Equal("Int32", result);
    }

    [Fact]
    public void ResolveRequestBodyReferenceId_ValidatesReferenceSchemaAgainstParameterType()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>();
        descriptor.Document.Components.RequestBodies["MyBody"] = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" }
                }
            }
        };

        var func = CreateFunctionInfo("function Test-Body { param([int]$Body) }", "Test-Body");
        var bodyParam = func.Parameters["Body"];
        var attr = new OpenApiRequestBodyRefAttribute { StatusCode = "200", ReferenceId = "MyBody" };

        var result = (string)InvokeInstance(descriptor, "ResolveRequestBodyReferenceId", [attr, bodyParam])!;

        Assert.Equal("MyBody", result);
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
        var method = instance.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == methodName)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(instance, args);
    }
}
