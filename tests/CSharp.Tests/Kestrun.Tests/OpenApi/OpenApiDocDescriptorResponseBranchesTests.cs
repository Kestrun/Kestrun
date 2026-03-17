using System.Reflection;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorResponseBranchesTests
{
    [Fact]
    public void GetKeyOverride_ReturnsKeyPropertyValue_WhenPresent()
    {
        var attr = new OpenApiResponseHeaderRefAttribute
        {
            Key = "x-correlation-id",
            ReferenceId = "Header1"
        };

        var key = (string?)InvokeStatic("GetKeyOverride", [attr]);

        Assert.Equal("x-correlation-id", key);
    }

    [Fact]
    public void CreateResponseFromAttribute_ReturnsFalse_ForUnsupportedAttribute()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        var response = new OpenApiResponse();

        var created = (bool)InvokePrivate(descriptor, "CreateResponseFromAttribute", [new OpenApiPathAttribute(), response, null!])!;

        Assert.False(created);
    }

    [Fact]
    public void CloneSchemaOrThrow_Throws_WhenSchemaNotFound()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "CloneSchemaOrThrow", ["MissingSchema"]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void CloneSchemaOrThrow_ReturnsClone_WhenSchemaExists()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        descriptor.Document.Components.Schemas["MySchema"] = new OpenApiSchema { Type = JsonSchemaType.String };

        var clone = (OpenApiSchema)InvokePrivate(descriptor, "CloneSchemaOrThrow", ["MySchema"])!;

        Assert.Equal(JsonSchemaType.String, clone.Type);
        Assert.NotSame(descriptor.Document.Components.Schemas["MySchema"], clone);
    }

    [Fact]
    public void ProcessResponseExampleRef_Throws_WhenKeyMissing()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var variable = new OpenApiComponentAnnotationScanner.AnnotatedVariable("Resp")
        {
            VariableType = typeof(string)
        };
        _ = InvokePrivate(descriptor, "ProcessResponseComponent", [variable, new OpenApiResponseComponentAttribute { Description = "resp" }]);

        var attr = new OpenApiResponseExampleRefAttribute
        {
            StatusCode = "default",
            Key = null!,
            ReferenceId = "Ex1"
        };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessResponseExampleRef", ["Resp", attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ProcessResponseHeaderRef_Throws_WhenResponseMissing()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var attr = new OpenApiResponseHeaderRefAttribute
        {
            StatusCode = "default",
            Key = "x-id",
            ReferenceId = "Header1"
        };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessResponseHeaderRef", ["MissingResponse", attr]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private static object? InvokePrivate(object target, string methodName, object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(target, args);
    }

    private static object? InvokeStatic(string methodName, object?[] args)
    {
        var method = typeof(OpenApiDocDescriptor).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(null, args);
    }
}
