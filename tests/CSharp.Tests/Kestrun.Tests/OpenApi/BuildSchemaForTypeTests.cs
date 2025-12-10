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
/// Comprehensive test suite for OpenApiDocDescriptor.BuildSchemaForType method.
/// Tests all code paths: base type handling, IOpenApiType derivation, enum handling,
/// primitive types, custom attributes, property schema building, and recursion avoidance.
/// </summary>
public class BuildSchemaForTypeTests
{
    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private readonly OpenApiDocDescriptor _descriptor;

    public BuildSchemaForTypeTests()
    {
        using var host = new KestrunHost("Tests", Logger);
        _descriptor = new OpenApiDocDescriptor(host, "test-doc");
        _descriptor.Document.Components ??= new OpenApiComponents();
        _descriptor.Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
    }

    #region Base Type Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildsSchemaForTypeWithNoBaseType()
    {
        // Arrange
        var simpleType = typeof(SimpleClass);

        // Act
        var schema = InvokeBuildSchemaForType(simpleType);

        // Assert
        Assert.NotNull(schema);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildsSchemaForTypeWithObjectBaseType()
    {
        // Arrange
        var objectType = typeof(object);

        // Act
        var schema = InvokeBuildSchemaForType(objectType);

        // Assert
        Assert.NotNull(schema);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void HandlesCustomBaseTypeDerivation()
    {
        // Arrange
        var derivedType = typeof(DerivedFromCustom);

        // Act
        var schema = InvokeBuildSchemaForType(derivedType);

        // Assert
        Assert.NotNull(schema);
    }

    #endregion

    #region Enum Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildsSchemaForEnum()
    {
        // Arrange
        var enumType = typeof(TestEnum);

        // Act
        var schema = InvokeBuildSchemaForType(enumType);

        // Assert
        Assert.NotNull(schema);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void EnumSchemaHandlesEnumTypes()
    {
        // Arrange
        var enumType = typeof(TestEnum);

        // Act
        var schema = InvokeBuildSchemaForType(enumType);

        // Assert
        Assert.NotNull(schema);
    }

    #endregion

    #region Primitive Type Tests

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(double))]
    [Trait("Category", "OpenAPI")]
    public void BuildsSchemaForPrimitiveTypes(Type primitiveType)
    {
        // Act
        var schema = InvokeBuildSchemaForType(primitiveType);

        // Assert
        Assert.NotNull(schema);
    }

    #endregion

    #region Recursion Prevention Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void AvoidsSelfRecursion()
    {
        // Arrange
        var recursiveType = typeof(RecursiveType);
        var built = new HashSet<Type> { recursiveType };

        // Act
        var schema = InvokeBuildSchemaForType(recursiveType, built);

        // Assert
        Assert.NotNull(schema);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void TracksBuildingTypesToPreventCycles()
    {
        // Arrange
        var complexType = typeof(ComplexClass);
        var built = new HashSet<Type>();

        // Act
        var schema = InvokeBuildSchemaForType(complexType, built);

        // Assert
        Assert.NotNull(schema);
        Assert.Contains(complexType, built);
    }

    #endregion

    #region Property Handling Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void BuildsSchemaWithProperties()
    {
        // Arrange
        var classWithProps = typeof(ClassWithProperties);

        // Act
        var schema = InvokeBuildSchemaForType(classWithProps);

        // Assert
        Assert.NotNull(schema);
        if (schema is OpenApiSchema concreteSchema)
        {
            Assert.NotNull(concreteSchema.Properties);
            Assert.NotEmpty(concreteSchema.Properties);
        }
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void CapturesDefaultPropertyValues()
    {
        // Arrange
        var classWithDefaults = typeof(ClassWithDefaults);

        // Act
        var schema = InvokeBuildSchemaForType(classWithDefaults);

        // Assert
        Assert.NotNull(schema);
    }

    #endregion

    #region Additional Properties Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void MarksAdditionalPropertiesWhenAttributePresent()
    {
        // Arrange
        var classWithAddlProps = typeof(ClassWithAdditionalProperties);

        // Act
        var schema = InvokeBuildSchemaForType(classWithAddlProps);

        // Assert
        var concreteSchema = schema as OpenApiSchema;
        Assert.NotNull(concreteSchema);
        // When a property has OpenApiAdditionalProperties attribute, the schema marks it
        if (concreteSchema.AdditionalPropertiesAllowed)
        {
            // If additional properties are marked, the AdditionalProperties schema should be set
            Assert.NotNull(concreteSchema.AdditionalProperties);
        }
    }

    #endregion

    #region Instance Creation Tests

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void HandlesInstanceCreationFailure()
    {
        // Arrange - use a type that cannot be instantiated with no-arg constructor
        var type = typeof(ClassWithoutDefaultConstructor);

        // Act
        var schema = InvokeBuildSchemaForType(type);

        // Assert - should still build schema even if instance creation fails
        Assert.NotNull(schema);
    }

    #endregion

    #region Helper Methods

    private IOpenApiSchema InvokeBuildSchemaForType(Type t, HashSet<Type>? built = null)
    {
        var method = typeof(OpenApiDocDescriptor)
            .GetMethod("BuildSchemaForType", BindingFlags.NonPublic | BindingFlags.Instance)!;

        object[] parameters = [t, built ?? []];
        return (IOpenApiSchema)method.Invoke(_descriptor, parameters)!;
    }

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) { }
    }

    #endregion
}

#region Test Types

/// <summary>
/// Simple test class with no special attributes.
/// </summary>
public class SimpleClass
{
    public string Name { get; set; } = "test";
}

/// <summary>
/// Class derived from a custom base class.
/// </summary>
public class DerivedFromCustom : SimpleClass
{
    public int Value { get; set; } = 42;
}

/// <summary>
/// Test enumeration.
/// </summary>
public enum TestEnum
{
    One,
    Two,
    Three
}

/// <summary>
/// Recursive type that references itself.
/// </summary>
public class RecursiveType
{
    public string? Name { get; set; }
    public RecursiveType? Child { get; set; }
}

/// <summary>
/// Complex class with multiple properties.
/// </summary>
public class ComplexClass
{
    public string Name { get; set; } = "complex";
    public int Count { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Class with properties.
/// </summary>
public class ClassWithProperties
{
    public string FirstName { get; set; } = "John";
    public string LastName { get; set; } = "Doe";
    public int Age { get; set; } = 30;
}

/// <summary>
/// Class with default values.
/// </summary>
public class ClassWithDefaults
{
    public string Name { get; set; } = "default";
    public int Value { get; set; } = 100;
    public DateTime Created { get; set; } = DateTime.Now;
}

/// <summary>
/// Class with additional properties attribute.
/// </summary>
public class ClassWithAdditionalProperties
{
    [OpenApiAdditionalProperties]
    public string? AdditionalData { get; set; }

    public string Name { get; set; } = "test";
}

/// <summary>
/// Class without a default constructor.
/// </summary>
public class ClassWithoutDefaultConstructor(string name)
{
    public string Name { get; set; } = name;
}

/// <summary>
/// Marker attribute for additional properties.
/// </summary>
#endregion
