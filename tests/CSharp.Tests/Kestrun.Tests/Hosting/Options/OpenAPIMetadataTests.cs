using Kestrun.Hosting.Options;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class OpenAPIMetadataTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void DefaultConstructor_InitializesProperties()
    {
        var metadata = new OpenAPIPathMetadata(pattern: "/test", mapOptions: new MapRouteOptions());

        Assert.Null(metadata.Summary);
        Assert.Null(metadata.Description);
        Assert.Null(metadata.OperationId);
        Assert.NotNull(metadata.Tags);
        Assert.Empty(metadata.Tags);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Summary_CanBeSet()
    {
        var metadata = new OpenAPIPathMetadata(pattern: "/test", mapOptions: new MapRouteOptions()) { Summary = "Test summary" };

        Assert.Equal("Test summary", metadata.Summary);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Description_CanBeSet()
    {
        var metadata = new OpenAPIPathMetadata(pattern: "/test", mapOptions: new MapRouteOptions()) { Description = "Test description" };

        Assert.Equal("Test description", metadata.Description);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void OperationId_CanBeSet()
    {
        var metadata = new OpenAPIPathMetadata(pattern: "/test", mapOptions: new MapRouteOptions()) { OperationId = "GetUsers" };

        Assert.Equal("GetUsers", metadata.OperationId);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Tags_CanBeSet()
    {
        var metadata = new OpenAPIPathMetadata(pattern: "/test", mapOptions: new MapRouteOptions()) { Tags = ["users", "api"] };

        Assert.Equal(2, metadata.Tags.Count);
        Assert.Contains("users", metadata.Tags);
        Assert.Contains("api", metadata.Tags);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RecordEquality_SameValues_ReturnsTrue()
    {
        // Record equality for reference types (List/Dictionary/etc) uses their Equals implementation.
        // For List<T> this is reference equality, so we normalize by sharing references.
        var sharedTags = new List<string>();
        var sharedDocumentIds = new List<string>();
        var mapOptions = new MapRouteOptions();

        var metadata1 = new OpenAPIPathMetadata(pattern: "/test", mapOptions: mapOptions)
        {
            Summary = "Test",
            OperationId = "Op1",
            // Normalize properties that would otherwise differ by reference
            Servers = null,
            Parameters = null,
            DocumentIds = sharedDocumentIds,
            Tags = sharedTags,
            Callbacks = null,
            SecuritySchemes = null,
            Extensions = null,
            RequestBody = null,
            Responses = null
        };
        var metadata2 = new OpenAPIPathMetadata(pattern: "/test", mapOptions: mapOptions)
        {
            Summary = "Test",
            OperationId = "Op1",
            Servers = null,
            Parameters = null,
            DocumentIds = sharedDocumentIds,
            Tags = sharedTags,
            Callbacks = null,
            SecuritySchemes = null,
            Extensions = null,
            RequestBody = null,
            Responses = null
        };

        Assert.Equal(metadata1, metadata2);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RecordEquality_DifferentValues_ReturnsFalse()
    {
        var metadata1 = new OpenAPIPathMetadata(pattern: "/test1", mapOptions: new MapRouteOptions()) { Summary = "Test1" };
        var metadata2 = new OpenAPIPathMetadata(pattern: "/test2", mapOptions: new MapRouteOptions()) { Summary = "Test2" };
        Assert.NotEqual(metadata1, metadata2);
    }
}
