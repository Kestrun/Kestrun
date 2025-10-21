using Kestrun.Hosting.Options;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class OpenAPIMetadataTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void DefaultConstructor_InitializesProperties()
    {
        var metadata = new OpenAPIMetadata(pattern: "/test");

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
        var metadata = new OpenAPIMetadata(pattern: "/test") { Summary = "Test summary" };

        Assert.Equal("Test summary", metadata.Summary);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Description_CanBeSet()
    {
        var metadata = new OpenAPIMetadata(pattern: "/test") { Description = "Test description" };

        Assert.Equal("Test description", metadata.Description);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void OperationId_CanBeSet()
    {
        var metadata = new OpenAPIMetadata(pattern: "/test") { OperationId = "GetUsers" };

        Assert.Equal("GetUsers", metadata.OperationId);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Tags_CanBeSet()
    {
        var metadata = new OpenAPIMetadata(pattern: "/test") { Tags = ["users", "api"] };

        Assert.Equal(2, metadata.Tags.Length);
        Assert.Contains("users", metadata.Tags);
        Assert.Contains("api", metadata.Tags);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RecordEquality_SameValues_ReturnsTrue()
    {
        var metadata1 = new OpenAPIMetadata(pattern: "/test") { Summary = "Test", OperationId = "Op1" };
        var metadata2 = new OpenAPIMetadata(pattern: "/test") { Summary = "Test", OperationId = "Op1" };

        Assert.Equal(metadata1, metadata2);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RecordEquality_DifferentValues_ReturnsFalse()
    {
        var metadata1 = new OpenAPIMetadata(pattern: "/test1") { Summary = "Test1" };
        var metadata2 = new OpenAPIMetadata(pattern: "/test2") { Summary = "Test2" };
        Assert.NotEqual(metadata1, metadata2);
    }
}
