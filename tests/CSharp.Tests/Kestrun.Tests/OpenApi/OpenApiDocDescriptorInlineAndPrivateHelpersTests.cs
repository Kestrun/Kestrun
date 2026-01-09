using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorInlineAndPrivateHelpersTests
{
    [Fact]
    public void TryGetInline_ReturnsFalse_WhenInlineComponentsAreNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        Assert.False(d.TryGetInline<OpenApiExample>("missing", OpenApiComponentKind.Examples, out _));
    }

    [Fact]
    public void TryGetComponent_ReturnsFalse_WhenComponentsAreNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        Assert.False(d.TryGetComponent<OpenApiExample>("missing", OpenApiComponentKind.Examples, out _));
    }

    [Fact]
    public void AddInlineExample_InvalidConflictResolution_ThrowsArgumentOutOfRange()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            d.AddInlineExample("ex", new OpenApiExample(), (OpenApiComponentConflictResolution)999));
    }

    [Fact]
    public void TryGetComponent_InvalidKind_ThrowsArgumentOutOfRange()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            d.TryGetComponent<OpenApiExample>("ex", (OpenApiComponentKind)999, out _));
    }

    [Fact]
    public void TryAddExample_AddsClone_WhenExampleIsInline()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddInlineExample("exInline", new OpenApiExample { Summary = "inline" });
        Assert.True(d.TryGetInline<OpenApiExample>("exInline", OpenApiComponentKind.Examples, out var inlineEx));

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "exInline",
            Inline = true
        };

        var added = InvokeTryAddExample(d, examples, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiExample>(examples["k"]);
        Assert.Equal("inline", stored.Summary);
        Assert.NotSame(inlineEx, stored);
    }

    [Fact]
    public void TryAddExample_AddsReference_WhenExampleIsComponent_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddComponentExample("exComp", new OpenApiExample { Summary = "component" });

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "exComp",
            Inline = false
        };

        var added = InvokeTryAddExample(d, examples, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiExampleReference>(examples["k"]);
        Assert.Equal("exComp", stored.Reference.Id);
    }

    [Fact]
    public void TryAddExample_ReturnsFalse_WhenMissing_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = false
        };

        var added = InvokeTryAddExample(d, examples, attr);

        Assert.False(added);
        Assert.False(examples.ContainsKey("k"));
    }

    [Fact]
    public void TryAddExample_Throws_WhenMissing_AndInlineTrue()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        var attr = new OpenApiResponseExampleRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = true
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeTryAddExample(d, examples, attr));
    }

    [Fact]
    public void TryAddLink_AddsClone_WhenLinkIsInline()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddInlineLink("lnkInline", new OpenApiLink
        {
            Description = "inline",
            OperationId = "op",
            Parameters = new Dictionary<string, RuntimeExpressionAnyWrapper>
            {
                ["id"] = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.FromObject("abc") }
            },
            RequestBody = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.FromObject(new { x = 1 }) }
        });

        Assert.True(d.TryGetInline<OpenApiLink>("lnkInline", OpenApiComponentKind.Links, out var inlineLnk));

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "lnkInline",
            Inline = true
        };

        var added = InvokeTryAddLink(d, links, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiLink>(links["k"]);
        Assert.Equal("inline", stored.Description);
        Assert.NotSame(inlineLnk, stored);
    }

    [Fact]
    public void TryAddLink_AddsReference_WhenLinkIsComponent_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        d.AddComponentLink("lnkComp", new OpenApiLink
        {
            Description = "component",
            OperationId = "op",
            Parameters = new Dictionary<string, RuntimeExpressionAnyWrapper>
            {
                ["id"] = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.FromObject("abc") }
            },
            RequestBody = new RuntimeExpressionAnyWrapper { Any = OpenApiJsonNodeFactory.FromObject(new { x = 1 }) }
        });

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "lnkComp",
            Inline = false
        };

        var added = InvokeTryAddLink(d, links, attr);

        Assert.True(added);
        var stored = Assert.IsType<OpenApiLinkReference>(links["k"]);
        Assert.Equal("lnkComp", stored.Reference.Id);
    }

    [Fact]
    public void TryAddLink_ReturnsFalse_WhenMissing_AndInlineFalse()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = false
        };

        var added = InvokeTryAddLink(d, links, attr);

        Assert.False(added);
        Assert.False(links.ContainsKey("k"));
    }

    [Fact]
    public void TryAddLink_Throws_WhenMissing_AndInlineTrue()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        var attr = new OpenApiResponseLinkRefAttribute
        {
            Key = "k",
            ReferenceId = "missing",
            Inline = true
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeTryAddLink(d, links, attr));
    }

    [Fact]
    public void ApplyResponseLinkAttribute_Throws_WhenStatusCodeIsNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var meta = new OpenAPIPathMetadata(new MapRouteOptions());
        var attr = new OpenApiResponseLinkRefAttribute
        {
            StatusCode = null!,
            Key = "k",
            ReferenceId = "x"
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeApplyResponseLinkAttribute(d, meta, attr));
    }

    [Fact]
    public void ApplyResponseLinkAttribute_Throws_WhenKeyIsNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var meta = new OpenAPIPathMetadata(new MapRouteOptions());
        var attr = new OpenApiResponseLinkRefAttribute
        {
            StatusCode = "200",
            Key = null!,
            ReferenceId = "x"
        };

        _ = Assert.Throws<InvalidOperationException>(() => InvokeApplyResponseLinkAttribute(d, meta, attr));
    }

    private static bool InvokeTryAddExample(OpenApiDocDescriptor d, IDictionary<string, IOpenApiExample> examples, IOpenApiExampleAttribute attr)
    {
        var m = typeof(OpenApiDocDescriptor).GetMethod("TryAddExample", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        object?[] args = [examples, attr];
        try
        {
            return (bool)m.Invoke(d, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static bool InvokeTryAddLink(OpenApiDocDescriptor d, IDictionary<string, IOpenApiLink> links, OpenApiResponseLinkRefAttribute attr)
    {
        var m = typeof(OpenApiDocDescriptor).GetMethod("TryAddLink", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        object?[] args = [links, attr];
        try
        {
            return (bool)m.Invoke(d, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static void InvokeApplyResponseLinkAttribute(OpenApiDocDescriptor d, OpenAPIPathMetadata meta, OpenApiResponseLinkRefAttribute attr)
    {
        var m = typeof(OpenApiDocDescriptor).GetMethod("ApplyResponseLinkAttribute", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(m);
        object?[] args = [meta, attr];
        try
        {
            _ = m.Invoke(d, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
