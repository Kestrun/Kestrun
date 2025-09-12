using Xunit;

namespace KestrunTests.Utility;

public class KestrunRuntimeApiAttributeTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void Attribute_StoresContexts_And_Properties()
    {
        var attr = new KestrunRuntimeApiAttribute(KestrunApiContext.Definition | KestrunApiContext.Route)
        {
            SafeForUntrusted = true,
            Notes = "hello"
        };

        Assert.True(attr.Contexts.HasFlag(KestrunApiContext.Definition));
        Assert.True(attr.Contexts.HasFlag(KestrunApiContext.Route));
        Assert.True(attr.SafeForUntrusted);
        Assert.Equal("hello", attr.Notes);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void ApiContext_ComposedFlags_Work()
    {
        Assert.Equal(KestrunApiContext.Schedule | KestrunApiContext.Definition, KestrunApiContext.ScheduleAndDefinition);
        Assert.True(KestrunApiContext.Everywhere.HasFlag(KestrunApiContext.Route));
        Assert.True(KestrunApiContext.Runtime.HasFlag(KestrunApiContext.Schedule));
    }
}
