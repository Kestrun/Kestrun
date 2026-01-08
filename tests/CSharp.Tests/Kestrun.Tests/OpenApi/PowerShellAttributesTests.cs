using System.Reflection;
using System.Management.Automation;
using Microsoft.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

public class PowerShellAttributesTests
{
    private sealed class AttributeModel
    {
        [ValidateRange(1, 10)]
        public int IntRange { get; set; }

        [ValidateSet("name", "price")]
        public string SortBy { get; set; } = "name";

        [ValidatePattern("^[0-9a-fA-F-]{36}$")]
        public string CorrelationId { get; set; } = string.Empty;

        [ValidateLength(2, 5)]
        public string ShortCode { get; set; } = string.Empty;

        [ValidateCount(1, 3)]
        public string[] Tags { get; set; } = [];

        [ValidateNotNullOrWhiteSpace]
        public string NotBlank { get; set; } = string.Empty;
    }

    private static PropertyInfo GetProp(string name) => typeof(AttributeModel).GetProperty(name)!;

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ApplyPowerShellAttributes_AppliesValidateRange()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        PowerShellAttributes.ApplyPowerShellAttributes(GetProp(nameof(AttributeModel.IntRange)), schema);

        Assert.Equal("1", schema.Minimum);
        Assert.Equal("10", schema.Maximum);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ApplyPowerShellAttributes_AppliesValidateSetAsEnum()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        PowerShellAttributes.ApplyPowerShellAttributes(GetProp(nameof(AttributeModel.SortBy)), schema);

        Assert.NotNull(schema.Enum);
        var values = schema.Enum!.Select(n => n.ToString().Trim('"')).ToArray();
        Assert.Contains("name", values);
        Assert.Contains("price", values);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ApplyPowerShellAttributes_AppliesValidatePattern()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        PowerShellAttributes.ApplyPowerShellAttributes(GetProp(nameof(AttributeModel.CorrelationId)), schema);

        Assert.Equal("^[0-9a-fA-F-]{36}$", schema.Pattern);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ApplyPowerShellAttributes_AppliesValidateLength()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        PowerShellAttributes.ApplyPowerShellAttributes(GetProp(nameof(AttributeModel.ShortCode)), schema);

        Assert.Equal(2, schema.MinLength);
        Assert.Equal(5, schema.MaxLength);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ApplyPowerShellAttributes_AppliesValidateCountForArrays()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array };
        PowerShellAttributes.ApplyPowerShellAttributes(GetProp(nameof(AttributeModel.Tags)), schema);

        Assert.Equal(1, schema.MinItems);
        Assert.Equal(3, schema.MaxItems);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ApplyPowerShellAttributes_AppliesValidateNotNullOrWhiteSpace()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        PowerShellAttributes.ApplyPowerShellAttributes(GetProp(nameof(AttributeModel.NotBlank)), schema);

        Assert.True(schema.MinLength is >= 1);
        Assert.Equal(@"\S", schema.Pattern);
    }
}
