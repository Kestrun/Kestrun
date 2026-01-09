using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorComponentsTests
{
    [Fact]
    public void AddComponentExample_Overwrite_ReplacesExisting()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddComponentExample("ex", new OpenApiExample { Summary = "one" }, OpenApiComponentConflictResolution.Overwrite);
        d.AddComponentExample("ex", new OpenApiExample { Summary = "two" }, OpenApiComponentConflictResolution.Overwrite);

        var components = d.Document.Components;
        Assert.NotNull(components);
        var examples = components.Examples;
        Assert.NotNull(examples);
        var stored = Assert.IsType<OpenApiExample>(examples["ex"]);
        Assert.Equal("two", stored.Summary);
    }

    [Fact]
    public void AddComponentExample_Ignore_KeepsExisting()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddComponentExample("ex", new OpenApiExample { Summary = "one" }, OpenApiComponentConflictResolution.Overwrite);
        d.AddComponentExample("ex", new OpenApiExample { Summary = "two" }, OpenApiComponentConflictResolution.Ignore);

        var components = d.Document.Components;
        Assert.NotNull(components);
        var examples = components.Examples;
        Assert.NotNull(examples);
        var stored = Assert.IsType<OpenApiExample>(examples["ex"]);
        Assert.Equal("one", stored.Summary);
    }

    [Fact]
    public void AddComponentExample_Error_ThrowsOnDuplicate()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddComponentExample("ex", new OpenApiExample(), OpenApiComponentConflictResolution.Overwrite);

        _ = Assert.Throws<InvalidOperationException>(() =>
            d.AddComponentExample("ex", new OpenApiExample(), OpenApiComponentConflictResolution.Error));
    }

    [Fact]
    public void AddComponentLink_ConflictResolution_Works()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddComponentLink("lnk", new OpenApiLink { Description = "one" }, OpenApiComponentConflictResolution.Overwrite);
        d.AddComponentLink("lnk", new OpenApiLink { Description = "two" }, OpenApiComponentConflictResolution.Ignore);

        var components = d.Document.Components;
        Assert.NotNull(components);
        var links = components.Links;
        Assert.NotNull(links);
        var stored = Assert.IsType<OpenApiLink>(links["lnk"]);
        Assert.Equal("one", stored.Description);

        _ = Assert.Throws<InvalidOperationException>(() =>
            d.AddComponentLink("lnk", new OpenApiLink(), OpenApiComponentConflictResolution.Error));
    }

    [Fact]
    public void TryGetComponent_ThrowsOnTypeMismatch()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddComponentExample("ex", new OpenApiExample(), OpenApiComponentConflictResolution.Overwrite);

        _ = Assert.Throws<InvalidOperationException>(() =>
            d.TryGetComponent<OpenApiSchema>("ex", OpenApiComponentKind.Examples, out _));
    }
}
