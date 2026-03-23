using System.Reflection;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorParameterPowerShellTests
{
    [Fact]
    public void ProcessPowerShellAttribute_AppliesConstraints_ToParameterSchema()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var parameter = (OpenApiParameter)InvokePrivate(descriptor, "GetOrCreateParameterItem", ["q", false])!;
        parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String };

        var psAttr = new InternalPowershellAttribute
        {
            MinLength = 2,
            MaxLength = 10,
            RegexPattern = "^[a-z]+$",
            MinRange = "1",
            MaxRange = "100",
            MinItems = 1,
            MaxItems = 5,
            AllowedValues = ["a", "b"],
            ValidateNotNullAttribute = true,
            ValidateNotNullOrWhiteSpaceAttribute = true
        };

        _ = InvokePrivate(descriptor, "ProcessPowerShellAttribute", ["q", psAttr]);

        var schema = Assert.IsType<OpenApiSchema>(parameter.Schema);
        Assert.Equal(2, schema.MinLength);
        Assert.Equal(10, schema.MaxLength);
        Assert.Equal("^[a-z]+$", schema.Pattern);
        Assert.Equal("1", schema.Minimum);
        Assert.Equal("100", schema.Maximum);
        Assert.Equal(1, schema.MinItems);
        Assert.Equal(5, schema.MaxItems);
        Assert.NotNull(schema.Enum);
        Assert.Equal(2, schema.Enum!.Count);
    }

    [Fact]
    public void ProcessPowerShellAttribute_Throws_WhenParameterHasNoSchemaOrContent()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = InvokePrivate(descriptor, "GetOrCreateParameterItem", ["missingShape", false]);

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessPowerShellAttribute", ["missingShape", new InternalPowershellAttribute { MinLength = 1 }]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ProcessPowerShellAttribute_AppliesToRequestBodyContentSchemas()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);

        var requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } }
            }
        };
        descriptor.Document.Components.RequestBodies["BodyA"] = requestBody;

        _ = InvokePrivate(descriptor, "ProcessPowerShellAttribute", ["BodyA", new InternalPowershellAttribute { MinLength = 3 }]);

        var schema = Assert.IsType<OpenApiSchema>(Assert.IsType<OpenApiMediaType>(requestBody.Content["application/json"]).Schema);
        Assert.Equal(3, schema.MinLength);
    }

    [Fact]
    public void ProcessPowerShellAttribute_Throws_WhenRequestBodyHasNoContent()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
        descriptor.Document.Components.RequestBodies["BodyNoContent"] = new OpenApiRequestBody();

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivate(descriptor, "ProcessPowerShellAttribute", ["BodyNoContent", new InternalPowershellAttribute { MaxLength = 5 }]));

        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private static object? InvokePrivate(object target, string methodName, object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(target, args);
    }
}
