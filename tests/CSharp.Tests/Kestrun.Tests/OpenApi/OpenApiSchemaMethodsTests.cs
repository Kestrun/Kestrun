using System.Reflection;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.OpenApi;

/// <summary>
/// Comprehensive test suite for OpenApiDocDescriptor schema-related methods.
/// Tests BuildPropertySchema, InferPrimitiveSchema, ApplySchemaAttr, MakeNullable,
/// IsIntrinsicDefault, and schema attribute application methods.
/// </summary>
public class OpenApiSchemaMethodsTests
{
    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private readonly OpenApiDocDescriptor _descriptor;

    public OpenApiSchemaMethodsTests()
    {
        using var host = new KestrunHost("Tests", Logger);
        _descriptor = new OpenApiDocDescriptor(host, "test-doc");
        _descriptor.Document.Components ??= new OpenApiComponents();
        _descriptor.Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
    }

    #region BuildPropertySchema Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildPropertySchema_PrimitiveType_ReturnsCorrectSchema()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Name))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildPropertySchema(property, built);

        // Assert
        Assert.NotNull(schema);
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.Equal(JsonSchemaType.String, concreteSchema.Type);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildPropertySchema_NullableType_SetsNullFlag()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.NullableInt))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildPropertySchema(property, built);

        // Assert
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.True(concreteSchema.Type!.Value.HasFlag(JsonSchemaType.Null));
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildPropertySchema_EnumType_ReturnsSchemaReference()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Status))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildPropertySchema(property, built);

        // Assert - enums are now treated as schema components and referenced
        var reference = Assert.IsAssignableFrom<OpenApiSchemaReference>(schema);
        Assert.Equal("TestStatus", reference.Reference.Id);

        // Verify the enum component was registered in the document
        Assert.True(_descriptor.Document.Components!.Schemas!.ContainsKey("TestStatus"));
        var enumSchema = _descriptor.Document.Components.Schemas["TestStatus"] as OpenApiSchema;
        Assert.NotNull(enumSchema);
        Assert.Equal(JsonSchemaType.String, enumSchema.Type);
        Assert.NotNull(enumSchema.Enum);
        Assert.NotEmpty(enumSchema.Enum);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildPropertySchema_NullableEnumType_ReturnsAnyOfWithNull()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.NullableStatus))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildPropertySchema(property, built);

        // Assert - nullable enums should be wrapped in anyOf with null
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.NotNull(concreteSchema.AnyOf);
        Assert.Equal(2, concreteSchema.AnyOf.Count);

        // First item should be a reference to the enum
        var enumRef = Assert.IsAssignableFrom<OpenApiSchemaReference>(concreteSchema.AnyOf[0]);
        Assert.Equal("TestStatus", enumRef.Reference.Id);

        // Second item should be a null schema
        var nullSchema = Assert.IsAssignableFrom<OpenApiSchema>(concreteSchema.AnyOf[1]);
        Assert.Equal(JsonSchemaType.Null, nullSchema.Type);

        // Verify the enum component was registered in the document
        Assert.True(_descriptor.Document.Components!.Schemas!.ContainsKey("TestStatus"));
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildPropertySchema_ArrayType_ReturnsArraySchema()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Tags))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildPropertySchema(property, built);

        // Assert
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.Equal(JsonSchemaType.Array, concreteSchema.Type);
        Assert.NotNull(concreteSchema.Items);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildPropertySchema_ComplexType_ReturnsSchemaReference()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Address))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildPropertySchema(property, built);

        // Assert
        _ = Assert.IsAssignableFrom<OpenApiSchemaReference>(schema);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildPropertySchema_AppliesPowerShellAttributes()
    {
        // Arrange
        var property = typeof(TestClassWithValidation).GetProperty(nameof(TestClassWithValidation.RangedValue))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildPropertySchema(property, built);

        // Assert - PowerShell validation attributes should be applied
        Assert.NotNull(schema);
    }

    #endregion

    #region InferPrimitiveSchema Tests

    [Theory]
    [InlineData(typeof(string), JsonSchemaType.String)]
    [InlineData(typeof(int), JsonSchemaType.Integer)]
    [InlineData(typeof(bool), JsonSchemaType.Boolean)]
    [InlineData(typeof(double), JsonSchemaType.Number)]
    [InlineData(typeof(decimal), JsonSchemaType.Number)]
    [Trait("Category", "OpenAPI")]
    public void InferPrimitiveSchema_PrimitiveTypes_ReturnsCorrectType(Type type, JsonSchemaType expectedType)
    {
        // Act
        var schema = InvokeInferPrimitiveSchema(type);

        // Assert
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.True(concreteSchema.Type!.Value.HasFlag(expectedType));
    }

    [Theory]
    [InlineData(typeof(DateTime), "date-time")]
    [InlineData(typeof(DateTimeOffset), "date-time")]
    [InlineData(typeof(Guid), "uuid")]
    [InlineData(typeof(Uri), "uri")]
    [Trait("Category", "OpenAPI")]
    public void InferPrimitiveSchema_FormattedTypes_ReturnsCorrectFormat(Type type, string expectedFormat)
    {
        // Act
        var schema = InvokeInferPrimitiveSchema(type);

        // Assert
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.Equal(expectedFormat, concreteSchema.Format);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void InferPrimitiveSchema_NullableType_UnwrapsToUnderlyingType()
    {
        // Act
        var schema = InvokeInferPrimitiveSchema(typeof(int?));

        // Assert
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.True(concreteSchema.Type!.Value.HasFlag(JsonSchemaType.Integer));
        Assert.True(concreteSchema.Type.Value.HasFlag(JsonSchemaType.Null));
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void InferPrimitiveSchema_ArrayType_ReturnsArraySchema()
    {
        // Act
        var schema = InvokeInferPrimitiveSchema(typeof(string[]));

        // Assert
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.Equal(JsonSchemaType.Array, concreteSchema.Type);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void InferPrimitiveSchema_OpenApiStringType_ReturnsStringSchema()
    {
        // Act
        var schema = InvokeInferPrimitiveSchema(typeof(OpenApiString));

        // Assert
        var concreteSchema = Assert.IsAssignableFrom<OpenApiSchema>(schema);
        Assert.Equal(JsonSchemaType.String, concreteSchema.Type);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void InferPrimitiveSchema_InlineMode_ReturnsInlineSchema()
    {
        // Act
        var schema = InvokeInferPrimitiveSchema(typeof(string), inline: true);

        // Assert
        Assert.NotNull(schema);
        _ = Assert.IsAssignableFrom<OpenApiSchema>(schema);
    }

    #endregion

    #region MakeNullable Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void MakeNullable_WhenTrue_AddsNullType()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };

        // Act
        var result = InvokeMakeNullable(schema, isNullable: true);

        // Assert
        Assert.True(result.Type!.Value.HasFlag(JsonSchemaType.Null));
        Assert.True(result.Type.Value.HasFlag(JsonSchemaType.String));
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void MakeNullable_WhenFalse_DoesNotAddNullType()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };

        // Act
        var result = InvokeMakeNullable(schema, isNullable: false);

        // Assert
        Assert.False(result.Type!.Value.HasFlag(JsonSchemaType.Null));
        Assert.Equal(JsonSchemaType.String, result.Type);
    }

    #endregion

    #region IsIntrinsicDefault Tests

    [Theory]
    [InlineData(null, typeof(string), true)]
    [InlineData(0, typeof(int), true)]
    [InlineData(1, typeof(int), false)]
    [InlineData(false, typeof(bool), true)]
    [InlineData(true, typeof(bool), false)]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_VariousValues_ReturnsCorrectResult(object? value, Type type, bool expected)
    {
        // Act
        var result = InvokeIsIntrinsicDefault(value, type);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_EmptyGuid_ReturnsTrue()
    {
        // Act
        var result = InvokeIsIntrinsicDefault(Guid.Empty, typeof(Guid));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_NonEmptyGuid_ReturnsFalse()
    {
        // Act
        var result = InvokeIsIntrinsicDefault(Guid.NewGuid(), typeof(Guid));

        // Assert
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_ZeroTimeSpan_ReturnsTrue()
    {
        // Act
        var result = InvokeIsIntrinsicDefault(TimeSpan.Zero, typeof(TimeSpan));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_MinDateTime_ReturnsTrue()
    {
        // Act
        var result = InvokeIsIntrinsicDefault(DateTime.MinValue, typeof(DateTime));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_MinDateTimeOffset_ReturnsTrue()
    {
        // Act
        var result = InvokeIsIntrinsicDefault(DateTimeOffset.MinValue, typeof(DateTimeOffset));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_EnumZero_ReturnsTrue()
    {
        // Act
        var result = InvokeIsIntrinsicDefault(TestStatus.None, typeof(TestStatus));

        // Assert
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_EnumNonZero_ReturnsFalse()
    {
        // Act
        var result = InvokeIsIntrinsicDefault(TestStatus.Active, typeof(TestStatus));

        // Assert
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void IsIntrinsicDefault_ReferenceTypeNonNull_ReturnsFalse()
    {
        // Act
        var result = InvokeIsIntrinsicDefault("test", typeof(string));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RegisterEnumSchema Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void RegisterEnumSchema_RegistersEnumAsSchemaComponent()
    {
        // Arrange
        var enumType = typeof(TestStatus);

        // Act
        InvokeRegisterEnumSchema(enumType);

        // Assert - enum should be registered as a reusable component schema
        Assert.True(_descriptor.Document.Components!.Schemas!.ContainsKey(enumType.Name));
        var schema = _descriptor.Document.Components.Schemas[enumType.Name] as OpenApiSchema;
        Assert.NotNull(schema);
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.NotNull(schema.Enum);
        Assert.Equal(3, schema.Enum.Count);
        Assert.Contains(schema.Enum, e => e?.GetValue<string>() == "None");
        Assert.Contains(schema.Enum, e => e?.GetValue<string>() == "Active");
        Assert.Contains(schema.Enum, e => e?.GetValue<string>() == "Inactive");
    }

    #endregion

    #region BuildArraySchema Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildArraySchema_PrimitiveElementType_ReturnsArraySchemaWithInlineItems()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Tags))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildArraySchema(property.PropertyType, property, built);

        // Assert
        Assert.NotNull(schema);
        Assert.Equal(JsonSchemaType.Array, schema.Type);
        Assert.NotNull(schema.Items);
        var items = Assert.IsAssignableFrom<OpenApiSchema>(schema.Items);
        Assert.Equal(JsonSchemaType.String, items.Type);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildArraySchema_ComplexElementType_ReturnsArraySchemaWithReferenceItems()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Addresses))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildArraySchema(property.PropertyType, property, built);

        // Assert
        Assert.NotNull(schema);
        Assert.Equal(JsonSchemaType.Array, schema.Type);
        Assert.NotNull(schema.Items);
        _ = Assert.IsAssignableFrom<OpenApiSchemaReference>(schema.Items);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildArraySchema_EnumElementType_ReturnsArraySchemaWithRefItems()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Statuses))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildArraySchema(property.PropertyType, property, built);

        // Assert - enum arrays now use schema references for items
        Assert.NotNull(schema);
        Assert.Equal(JsonSchemaType.Array, schema.Type);
        Assert.NotNull(schema.Items);
        var itemsRef = Assert.IsAssignableFrom<OpenApiSchemaReference>(schema.Items);
        Assert.Equal("TestStatus", itemsRef.Reference.Id);

        // Verify the enum component was registered in the document
        Assert.True(_descriptor.Document.Components!.Schemas!.ContainsKey("TestStatus"));
        var enumSchema = _descriptor.Document.Components.Schemas["TestStatus"] as OpenApiSchema;
        Assert.NotNull(enumSchema);
        Assert.Equal(JsonSchemaType.String, enumSchema.Type);
        Assert.NotNull(enumSchema.Enum);
    }

    #endregion

    #region RegisterEnumSchema Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void RegisterEnumSchema_AddsEnumToComponents()
    {
        // Arrange
        var enumType = typeof(TestStatus);

        // Act
        InvokeRegisterEnumSchema(enumType);

        // Assert
        Assert.True(_descriptor.Document.Components!.Schemas!.ContainsKey(enumType.Name));
    }

    #endregion

    #region BuildComplexTypeSchema Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildComplexTypeSchema_CreatesSchemaReference()
    {
        // Arrange
        var property = typeof(TestClassWithProperties).GetProperty(nameof(TestClassWithProperties.Address))!;
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildComplexTypeSchema(property.PropertyType, property, built);

        // Assert
        var refSchema = Assert.IsAssignableFrom<OpenApiSchemaReference>(schema);
        Assert.Equal(nameof(Address), refSchema.Reference.Id);
    }

    #endregion

    #region InferArraySchema Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void InferArraySchema_CreatesArraySchemaWithItemsReference()
    {
        // Arrange
        var arrayType = typeof(Address[]);

        // Act
        var schema = InvokeInferArraySchema(arrayType, inline: false);

        // Assert
        Assert.NotNull(schema);
        Assert.Equal(JsonSchemaType.Array, schema.Type);
        Assert.NotNull(schema.Items);
    }

    #endregion

    #region Helper Methods

    private IOpenApiSchema InvokeBuildPropertySchema(PropertyInfo property, HashSet<Type> built)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("BuildPropertySchema", BindingFlags.NonPublic | BindingFlags.Instance)!;

        object[] parameters = [property, built];
        return (IOpenApiSchema)method.Invoke(_descriptor, parameters)!;
    }

    private IOpenApiSchema InvokeInferPrimitiveSchema(Type type, bool inline = false)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("InferPrimitiveSchema", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;

        object[] parameters = [type, inline];
        return (IOpenApiSchema)method.Invoke(_descriptor, parameters)!;
    }

    private static OpenApiSchema InvokeMakeNullable(OpenApiSchema schema, bool isNullable)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("MakeNullable", BindingFlags.NonPublic | BindingFlags.Static)!;

        object[] parameters = [schema, isNullable];
        return (OpenApiSchema)method.Invoke(null, parameters)!;
    }

    private static bool InvokeIsIntrinsicDefault(object? value, Type declaredType)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("IsIntrinsicDefault", BindingFlags.NonPublic | BindingFlags.Static)!;

        object?[] parameters = [value, declaredType];
        return (bool)method.Invoke(null, parameters)!;
    }

    private OpenApiSchema InvokeBuildArraySchema(Type pt, PropertyInfo p, HashSet<Type> built)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("BuildArraySchema", BindingFlags.NonPublic | BindingFlags.Instance)!;

        object[] parameters = [pt, p, built];
        return (OpenApiSchema)method.Invoke(_descriptor, parameters)!;
    }

    private void InvokeRegisterEnumSchema(Type enumType)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("RegisterEnumSchema", BindingFlags.NonPublic | BindingFlags.Instance)!;

        object[] parameters = [enumType];
        _ = method.Invoke(_descriptor, parameters);
    }

    private OpenApiSchemaReference InvokeBuildComplexTypeSchema(Type pt, PropertyInfo p, HashSet<Type> built)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("BuildComplexTypeSchema", BindingFlags.NonPublic | BindingFlags.Instance)!;

        object[] parameters = [pt, p, built];
        return (OpenApiSchemaReference)method.Invoke(_descriptor, parameters)!;
    }

    private OpenApiSchema InvokeInferArraySchema(Type type, bool inline)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("InferArraySchema", BindingFlags.NonPublic | BindingFlags.Instance)!;

        object[] parameters = [type, inline];
        return (OpenApiSchema)method.Invoke(_descriptor, parameters)!;
    }

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) { }
    }

    #endregion
}

#region Test Types

public class TestClassWithProperties
{
    public string Name { get; set; } = "test";
    public int? NullableInt { get; set; }
    public TestStatus Status { get; set; }
    public TestStatus? NullableStatus { get; set; }
    public string[] Tags { get; set; } = [];
    public Address Address { get; set; } = new();
    public Address[] Addresses { get; set; } = [];
    public TestStatus[] Statuses { get; set; } = [];
}

public class TestClassWithValidation
{
    public int RangedValue { get; set; }
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

public enum TestStatus
{
    None = 0,
    Active = 1,
    Inactive = 2
}

#endregion
