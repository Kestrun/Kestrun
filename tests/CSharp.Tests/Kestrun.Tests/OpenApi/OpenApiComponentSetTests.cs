using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiComponentSetTests
{
    [Fact]
    public void Defaults_AreEmptyNonNullLists()
    {
        var set = new OpenApiComponentSet();

        Assert.NotNull(set.SchemaTypes);
        Assert.NotNull(set.ParameterTypes);
        Assert.NotNull(set.ResponseTypes);
        Assert.NotNull(set.ExampleTypes);
        Assert.NotNull(set.RequestBodyTypes);
        Assert.NotNull(set.HeaderTypes);
        Assert.NotNull(set.LinkTypes);
        Assert.NotNull(set.CallbackTypes);
        Assert.NotNull(set.PathItemTypes);
        Assert.NotNull(set.SecuritySchemeTypes);
        Assert.NotNull(set.MediaType);

        Assert.Empty(set.SchemaTypes);
        Assert.Empty(set.ParameterTypes);
        Assert.Empty(set.ResponseTypes);
        Assert.Empty(set.ExampleTypes);
        Assert.Empty(set.RequestBodyTypes);
        Assert.Empty(set.HeaderTypes);
        Assert.Empty(set.LinkTypes);
        Assert.Empty(set.CallbackTypes);
        Assert.Empty(set.PathItemTypes);
        Assert.Empty(set.SecuritySchemeTypes);
        Assert.Empty(set.MediaType);
    }
}
