using System.Reflection;
using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorMergeAttributesTests
{
    [Fact]
    public void MergeSchemaAttributes_ReturnsNull_WhenInputIsNull()
    {
        var merged = InvokeMergeSchemaAttributes(null);

        Assert.Null(merged);
    }

    [Fact]
    public void MergeSchemaAttributes_ReturnsNull_WhenInputIsEmpty()
    {
        var merged = InvokeMergeSchemaAttributes([]);

        Assert.Null(merged);
    }

    [Fact]
    public void MergeSchemaAttributes_ReturnsSameInstance_WhenSingleAttribute()
    {
        var attr = new OpenApiPropertyAttribute
        {
            Title = "single"
        };

        var merged = InvokeMergeSchemaAttributes([attr]);

        Assert.Same(attr, merged);
    }

    [Fact]
    public void MergeSchemaAttributes_MergesValues_UsingDocumentedRules()
    {
        var first = new OpenApiPropertyAttribute
        {
            Title = "first-title",
            Description = "first-description",
            Format = "uuid",
            Pattern = "^a$",
            Maximum = "10",
            Minimum = "1",
            Enum = ["A", "B"],
            Default = "first-default",
            Example = "first-example",
            MaxLength = 20,
            MinLength = -1,
            MaxItems = 5,
            MinItems = -1,
            MultipleOf = 2m,
            Nullable = true,
            WriteOnly = true,
            UniqueItems = true,
            ExclusiveMinimum = true,
            Type = OaSchemaType.String,
            RequiredProperties = ["id", "name"],
            XmlName = "item",
            XmlNamespace = "urn:first",
            XmlPrefix = "f",
            XmlWrapped = true
        };

        var second = new OpenApiPropertyAttribute
        {
            Title = "second-title",
            Description = " ",
            Format = "",
            Pattern = "^b$",
            Maximum = "",
            Minimum = "2",
            Enum = ["C"],
            Default = 42,
            Example = "second-example",
            MaxLength = -1,
            MinLength = 3,
            MaxItems = -1,
            MinItems = 1,
            MultipleOf = 5m,
            ReadOnly = true,
            Deprecated = true,
            ExclusiveMaximum = true,
            Type = OaSchemaType.None,
            RequiredProperties = ["name", "email"],
            XmlName = "second-item",
            XmlPrefix = " ",
            XmlAttribute = true
        };

        var merged = Assert.IsType<OpenApiPropertyAttribute>(InvokeMergeSchemaAttributes([first, second]));

        Assert.Equal("second-title", merged.Title);
        Assert.Equal("first-description", merged.Description);
        Assert.Equal("uuid", merged.Format);
        Assert.Equal("^b$", merged.Pattern);
        Assert.Equal("10", merged.Maximum);
        Assert.Equal("2", merged.Minimum);

        Assert.Equal(["A", "B", "C"], merged.Enum);
        Assert.Equal(42, merged.Default);
        Assert.Equal("second-example", merged.Example);

        Assert.Equal(20, merged.MaxLength);
        Assert.Equal(3, merged.MinLength);
        Assert.Equal(5, merged.MaxItems);
        Assert.Equal(1, merged.MinItems);
        Assert.Equal(5m, merged.MultipleOf);

        Assert.True(merged.Nullable);
        Assert.True(merged.ReadOnly);
        Assert.True(merged.WriteOnly);
        Assert.True(merged.Deprecated);
        Assert.True(merged.UniqueItems);
        Assert.True(merged.ExclusiveMaximum);
        Assert.True(merged.ExclusiveMinimum);

        Assert.Equal(OaSchemaType.String, merged.Type);
        var requiredProperties = Assert.IsType<string[]>(merged.RequiredProperties);
        Assert.Equal(["id", "name", "email"], requiredProperties);

        Assert.Equal("second-item", merged.XmlName);
        Assert.Equal("urn:first", merged.XmlNamespace);
        Assert.Equal("f", merged.XmlPrefix);
        Assert.True(merged.XmlAttribute);
        Assert.True(merged.XmlWrapped);
    }

    [Fact]
    public void MergeSchemaAttributes_UsesLastNonNoneType_WhenMultipleTypeOverridesExist()
    {
        var first = new OpenApiPropertyAttribute { Type = OaSchemaType.String };
        var second = new OpenApiPropertyAttribute { Type = OaSchemaType.None };
        var third = new OpenApiPropertyAttribute { Type = OaSchemaType.Integer };

        var merged = Assert.IsType<OpenApiPropertyAttribute>(InvokeMergeSchemaAttributes([first, second, third]));

        Assert.Equal(OaSchemaType.Integer, merged.Type);
    }

    private static OpenApiPropertyAttribute? InvokeMergeSchemaAttributes(OpenApiPropertyAttribute[]? attrs)
    {
        var method = typeof(OpenApiDocDescriptor).GetMethod(
            "MergeSchemaAttributes",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        object?[] args = [attrs];
        try
        {
            return (OpenApiPropertyAttribute?)method.Invoke(null, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
