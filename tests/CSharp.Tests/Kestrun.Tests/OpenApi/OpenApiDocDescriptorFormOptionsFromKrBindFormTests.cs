using System.Reflection;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.OpenApi;

public class OpenApiDocDescriptorFormOptionsFromKrBindFormTests
{
    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void GeneratesFormOptionAndNestedRulesFromKrBindFormModel()
    {
        using var host = new KestrunHost("Tests", Logger);
        var descriptor = new OpenApiDocDescriptor(host, "test-doc");
        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        var components = new OpenApiComponentSet
        {
            SchemaTypes =
            [
                typeof(NestedMultipartRequest),
                typeof(NestedParts),
                typeof(OuterControl),
            ]
        };

        // Build schemas (mirrors normal generation flow).
        var generateComponents = typeof(OpenApiDocDescriptor)
            .GetMethod("GenerateComponents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _ = generateComponents.Invoke(descriptor, [components]);

        // Build form options after schema generation.
        var addFormOptions = typeof(OpenApiDocDescriptor)
            .GetMethod("AddFormOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _ = addFormOptions.Invoke(descriptor, [components]);

        Assert.True(host.Runtime.FormOptions.TryGetValue(typeof(NestedMultipartRequest).FullName!, out var options));
        Assert.NotNull(options);

        // Root: Outer + NestedParts container, plus nested Text/Json rules.
        Assert.Equal(4, options!.Rules.Count);

        var container = options.Rules.Single(r => string.Equals(r.Name, nameof(NestedParts), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, container.NestedRules.Count);
        Assert.Contains(container.NestedRules, r => string.Equals(r.Name, "Text", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(container.NestedRules, r => string.Equals(r.Name, "Json", StringComparison.OrdinalIgnoreCase));

        var text = options.Rules.Single(r => string.Equals(r.Name, "Text", StringComparison.OrdinalIgnoreCase));
        var json = options.Rules.Single(r => string.Equals(r.Name, "Json", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(nameof(NestedParts), text.Scope);
        Assert.Equal(nameof(NestedParts), json.Scope);
    }

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) { }
    }

    [OpenApiSchemaComponent(Description = "Nested multipart request body.")]
    private sealed class OuterControl
    {
        [OpenApiPatternProperties]
        public object? AdditionalProperties { get; set; }
    }

    [OpenApiSchemaComponent(Description = "Inner nested multipart payload.")]
    private sealed class NestedParts
    {
        [KrPart(Required = true, MaxBytes = 1024, ContentTypes = ["text/plain"])]
        [OpenApiProperty(Description = "Inner text part.")]
        public string Text { get; set; } = string.Empty;

        [KrPart(Required = true, MaxBytes = 4096, ContentTypes = ["application/json"])]
        [OpenApiProperty(Description = "Inner JSON part.")]
        public string Json { get; set; } = string.Empty;
    }

    [OpenApiSchemaComponent(Description = "Nested multipart request body.")]
    [KrBindForm(MaxNestingDepth = 1)]
    private sealed class NestedMultipartRequest
    {
        [KrPart(Required = true, MaxBytes = 1024, ContentTypes = ["application/json"])]
        [OpenApiProperty(Description = "Outer JSON control object.")]
        public OuterControl Outer { get; set; } = new();

        [KrPart(Required = true, MaxBytes = 1048576, ContentTypes = ["multipart/mixed"])]
        [OpenApiProperty(Description = "Nested multipart container.")]
        public NestedParts[] Nested { get; set; } = [];
    }
}
