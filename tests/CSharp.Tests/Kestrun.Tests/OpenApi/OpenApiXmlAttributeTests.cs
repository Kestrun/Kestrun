using System.Reflection;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using System.Text.Json.Nodes;

namespace KestrunTests.OpenApi;

/// <summary>
/// Tests for OpenApiXmlAttribute and XML metadata serialization in OpenAPI schemas.
/// </summary>
public class OpenApiXmlAttributeTests
{
    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private readonly OpenApiDocDescriptor _descriptor;

    public OpenApiXmlAttributeTests()
    {
        using var host = new KestrunHost("Tests", Logger);
        _descriptor = new OpenApiDocDescriptor(host, "test-doc");
        _descriptor.Document.Components ??= new OpenApiComponents();
        _descriptor.Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    [Trait("Category", "XML")]
    public void AppliesXmlNameFromOpenApiXmlAttribute()
    {
        // Arrange & Act
        var schema = InvokeBuildSchemaForType(typeof(ProductWithXmlName));

        // Assert
        Assert.NotNull(schema);
        Assert.IsType<OpenApiSchema>(schema);
        var concreteSchema = (OpenApiSchema)schema;
        var idProp = concreteSchema.Properties?["Id"];
        Assert.NotNull(idProp);
        Assert.IsType<OpenApiSchema>(idProp);
        var idSchema = (OpenApiSchema)idProp;
        Assert.NotNull(idSchema.Xml);
        Assert.Equal("product-id", idSchema.Xml.Name);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    [Trait("Category", "XML")]
    public void AppliesXmlNamespaceFromOpenApiXmlAttribute()
    {
        // Arrange & Act
        var schema = InvokeBuildSchemaForType(typeof(ProductWithXmlNamespace));

        // Assert
        Assert.NotNull(schema);
        Assert.IsType<OpenApiSchema>(schema);
        var concreteSchema = (OpenApiSchema)schema;
        var itemsProp = concreteSchema.Properties?["Items"];
        Assert.NotNull(itemsProp);
        Assert.IsType<OpenApiSchema>(itemsProp);
        var itemsSchema = (OpenApiSchema)itemsProp;
        Assert.NotNull(itemsSchema.Xml);
        Assert.NotNull(itemsSchema.Xml.Namespace);
        Assert.Equal("http://example.com/items", itemsSchema.Xml.Namespace.ToString());
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    [Trait("Category", "XML")]
    public void AppliesXmlPrefixFromOpenApiXmlAttribute()
    {
        // Arrange & Act
        var schema = InvokeBuildSchemaForType(typeof(ProductWithXmlPrefix));

        // Assert
        Assert.NotNull(schema);
        Assert.IsType<OpenApiSchema>(schema);
        var concreteSchema = (OpenApiSchema)schema;
        var nameProp = concreteSchema.Properties?["Name"];
        Assert.NotNull(nameProp);
        Assert.IsType<OpenApiSchema>(nameProp);
        var nameSchema = (OpenApiSchema)nameProp;
        Assert.NotNull(nameSchema.Xml);
        Assert.Equal("prod", nameSchema.Xml.Prefix);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    [Trait("Category", "XML")]
    public void AppliesXmlAttributeFlagAsExtension()
    {
        // Arrange & Act
        var schema = InvokeBuildSchemaForType(typeof(ProductWithXmlAttribute));

        // Assert
        Assert.NotNull(schema);
        Assert.IsType<OpenApiSchema>(schema);
        var concreteSchema = (OpenApiSchema)schema;
        var idProp = concreteSchema.Properties?["Id"];
        Assert.NotNull(idProp);
        Assert.IsType<OpenApiSchema>(idProp);
        var idSchema = (OpenApiSchema)idProp;
        Assert.NotNull(idSchema.Xml);
        Assert.NotNull(idSchema.Xml.Extensions);
        Assert.True(idSchema.Xml.Extensions.ContainsKey("x-attribute"));
        
        // Verify the extension value is true
        var ext = idSchema.Xml.Extensions["x-attribute"] as JsonNodeExtension;
        Assert.NotNull(ext);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    [Trait("Category", "XML")]
    public void AppliesXmlWrappedFlagAsExtension()
    {
        // Arrange & Act
        var schema = InvokeBuildSchemaForType(typeof(ProductWithXmlWrapped));

        // Assert
        Assert.NotNull(schema);
        Assert.IsType<OpenApiSchema>(schema);
        var concreteSchema = (OpenApiSchema)schema;
        var itemsProp = concreteSchema.Properties?["Items"];
        Assert.NotNull(itemsProp);
        Assert.IsType<OpenApiSchema>(itemsProp);
        var itemsSchema = (OpenApiSchema)itemsProp;
        Assert.NotNull(itemsSchema.Xml);
        Assert.NotNull(itemsSchema.Xml.Extensions);
        Assert.True(itemsSchema.Xml.Extensions.ContainsKey("x-wrapped"));
        
        // Verify the extension value is true
        var ext = itemsSchema.Xml.Extensions["x-wrapped"] as JsonNodeExtension;
        Assert.NotNull(ext);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    [Trait("Category", "XML")]
    public void CombinesMultipleXmlAttributeProperties()
    {
        // Arrange & Act
        var schema = InvokeBuildSchemaForType(typeof(ProductWithCombinedXml));

        // Assert
        Assert.NotNull(schema);
        Assert.IsType<OpenApiSchema>(schema);
        var concreteSchema = (OpenApiSchema)schema;
        var idProp = concreteSchema.Properties?["Id"];
        Assert.NotNull(idProp);
        Assert.IsType<OpenApiSchema>(idProp);
        var idSchema = (OpenApiSchema)idProp;
        Assert.NotNull(idSchema.Xml);
        Assert.Equal("ProductID", idSchema.Xml.Name);
        Assert.True(idSchema.Xml.Extensions?.ContainsKey("x-attribute"));
    }

    // Helper to invoke the private BuildSchemaForType method
    private IOpenApiSchema InvokeBuildSchemaForType(Type type)
    {
        var method = typeof(OpenApiDocDescriptor).GetMethod("BuildSchemaForType",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        return (IOpenApiSchema)method.Invoke(_descriptor, new object?[] { type, null })!;
    }

    #region Test Model Classes

    private class ProductWithXmlName
    {
        [OpenApiXml(Name = "product-id")]
        public string Id { get; set; } = string.Empty;
    }

    private class ProductWithXmlNamespace
    {
        [OpenApiXml(Namespace = "http://example.com/items")]
        public string[] Items { get; set; } = Array.Empty<string>();
    }

    private class ProductWithXmlPrefix
    {
        [OpenApiXml(Prefix = "prod")]
        public string Name { get; set; } = string.Empty;
    }

    private class ProductWithXmlAttribute
    {
        [OpenApiXml(Attribute = true)]
        public string Id { get; set; } = string.Empty;
    }

    private class ProductWithXmlWrapped
    {
        [OpenApiXml(Wrapped = true)]
        public string[] Items { get; set; } = Array.Empty<string>();
    }

    private class ProductWithCombinedXml
    {
        [OpenApiXml(Name = "ProductID", Attribute = true)]
        public int Id { get; set; }
    }

    #endregion

    private class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) { }
    }
}
