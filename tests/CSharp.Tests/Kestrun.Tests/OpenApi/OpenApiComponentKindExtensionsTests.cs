using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiComponentKindExtensionsTests
{
    [Theory]
    [InlineData(OpenApiComponentKind.Schemas, "schema")]
    [InlineData(OpenApiComponentKind.Responses, "response")]
    [InlineData(OpenApiComponentKind.Parameters, "parameter")]
    [InlineData(OpenApiComponentKind.Examples, "example")]
    [InlineData(OpenApiComponentKind.RequestBodies, "request body")]
    [InlineData(OpenApiComponentKind.Headers, "header")]
    [InlineData(OpenApiComponentKind.SecuritySchemes, "security scheme")]
    [InlineData(OpenApiComponentKind.Links, "link")]
    [InlineData(OpenApiComponentKind.Callbacks, "callback")]
    [InlineData(OpenApiComponentKind.PathItems, "path item")]
    [InlineData(OpenApiComponentKind.MediaTypes, "media type")]
    public void ToInlineLabel_MapsKnownKinds(OpenApiComponentKind kind, string expected) => Assert.Equal(expected, kind.ToInlineLabel());

    [Theory]
    [InlineData(OpenApiComponentKind.Examples, true)]
    [InlineData(OpenApiComponentKind.Links, true)]
    [InlineData(OpenApiComponentKind.Parameters, true)]
    [InlineData(OpenApiComponentKind.Headers, true)]
    [InlineData(OpenApiComponentKind.MediaTypes, true)]
    [InlineData(OpenApiComponentKind.Schemas, false)]
    [InlineData(OpenApiComponentKind.Responses, false)]
    [InlineData(OpenApiComponentKind.RequestBodies, false)]
    [InlineData(OpenApiComponentKind.SecuritySchemes, false)]
    [InlineData(OpenApiComponentKind.Callbacks, false)]
    [InlineData(OpenApiComponentKind.PathItems, false)]
    public void SupportsInline_MatchesSpec(OpenApiComponentKind kind, bool expected) => Assert.Equal(expected, kind.SupportsInline());
}
