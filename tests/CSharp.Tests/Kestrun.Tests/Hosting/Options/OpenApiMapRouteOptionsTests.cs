using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class OpenApiMapRouteOptionsTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_SetsDefaultPattern_WhenNull()
    {
        var map = new MapRouteOptions { Pattern = null };
        var options = new OpenApiMapRouteOptions(map);
        Assert.Equal("/openapi/{version}/openapi.{format}", options.MapOptions.Pattern);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DocId_NonDefault_RewritesPattern_WhenDefaultPattern()
    {
        var map = new MapRouteOptions { Pattern = null };
        var options = new OpenApiMapRouteOptions(map)
        {
            DocId = "custom"
        };

        Assert.Equal("/openapi/{documentId}/{version}/openapi.{format}", options.MapOptions.Pattern);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DocId_DoesNotRewritePattern_WhenCustomPatternAlreadySet()
    {
        var map = new MapRouteOptions { Pattern = "/custom/openapi" };
        var options = new OpenApiMapRouteOptions(map)
        {
            DocId = "custom"
        };

        Assert.Equal("/custom/openapi", options.MapOptions.Pattern);
        Assert.Equal("custom", options.DocId);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DocId_DefaultValue_DoesNotRewritePattern()
    {
        var map = new MapRouteOptions { Pattern = null };
        var options = new OpenApiMapRouteOptions(map)
        {
            DocId = OpenApiDocDescriptor.DefaultDocumentationId
        };

        Assert.Equal("/openapi/{version}/openapi.{format}", options.MapOptions.Pattern);
    }
}
