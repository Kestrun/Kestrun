using Kestrun.Forms;
using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiSchemaDiscoveryTests
{
    [OpenApiSchemaComponent(Description = "Test schema component")]
    private sealed class TestSchemaComponent
    {
        public string? Name { get; set; }
    }

    [Fact]
    [Trait("Category", "OpenApi")]
    public void GetOpenApiTypesAuto_ExcludesBaseFormPayloadTypes_ButIncludesUserSchemaComponents()
    {
        var components = OpenApiSchemaDiscovery.GetOpenApiTypesAuto();

        Assert.DoesNotContain(typeof(KrFormData), components.SchemaTypes);
        Assert.DoesNotContain(typeof(KrMultipart), components.SchemaTypes);

        Assert.Contains(typeof(TestSchemaComponent), components.SchemaTypes);
    }
}
