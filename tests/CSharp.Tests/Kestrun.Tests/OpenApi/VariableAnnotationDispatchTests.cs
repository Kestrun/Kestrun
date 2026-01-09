using System.Reflection;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.OpenApi;

public class VariableAnnotationDispatchTests
{
    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            // discard
        }
    }

    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private static void InvokeProcessVariableAnnotations(OpenApiDocDescriptor descriptor, Dictionary<string, OpenApiComponentAnnotationScanner.AnnotatedVariable>? annotations)
    {
        var method = typeof(OpenApiDocDescriptor).GetMethod(
            "ProcessVariableAnnotations",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        _ = method.Invoke(descriptor, [annotations]);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ProcessVariableAnnotations_DoesNotThrowOnNullOrEmpty()
    {
        using var host = new KestrunHost("Tests", Logger);
        var descriptor = new OpenApiDocDescriptor(host, "test-doc");
        descriptor.Document.Components ??= new OpenApiComponents();

        InvokeProcessVariableAnnotations(descriptor, null);
        InvokeProcessVariableAnnotations(descriptor, []);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ProcessVariableAnnotations_CreatesParameterAndAppliesPowerShellConstraints()
    {
        using var host = new KestrunHost("Tests", Logger);
        var descriptor = new OpenApiDocDescriptor(host, "test-doc");
        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);

        var annotated = new OpenApiComponentAnnotationScanner.AnnotatedVariable("limit")
        {
            VariableType = typeof(int),
            InitialValue = 5,
            NoDefault = false
        };

        annotated.Annotations.Add(new OpenApiParameterComponent
        {
            In = OaParameterLocation.Query,
            Required = false
        });

        annotated.Annotations.Add(new InternalPowershellAttribute
        {
            MinRange = "1",
            MaxRange = "10"
        });

        var dict = new Dictionary<string, OpenApiComponentAnnotationScanner.AnnotatedVariable>(StringComparer.Ordinal)
        {
            ["limit"] = annotated
        };

        InvokeProcessVariableAnnotations(descriptor, dict);

        Assert.NotNull(descriptor.Document.Components.Parameters);
        Assert.True(descriptor.Document.Components.Parameters.ContainsKey("limit"));

        var param = Assert.IsType<OpenApiParameter>(descriptor.Document.Components.Parameters["limit"]);
        var schema = Assert.IsType<OpenApiSchema>(param.Schema);

        Assert.Equal("1", schema.Minimum);
        Assert.Equal("10", schema.Maximum);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void DispatchComponentAnnotations_ThrowsIfPowerShellAttributeAppearsBeforeParameterComponent()
    {
        using var host = new KestrunHost("Tests", Logger);
        var descriptor = new OpenApiDocDescriptor(host, "test-doc");
        descriptor.Document.Components ??= new OpenApiComponents();

        var annotated = new OpenApiComponentAnnotationScanner.AnnotatedVariable("limit")
        {
            VariableType = typeof(int),
            InitialValue = 5,
            NoDefault = false
        };

        annotated.Annotations.Add(new InternalPowershellAttribute { MinRange = "1" });
        annotated.Annotations.Add(new OpenApiParameterComponent { In = OaParameterLocation.Query });

        var dict = new Dictionary<string, OpenApiComponentAnnotationScanner.AnnotatedVariable>(StringComparer.Ordinal)
        {
            ["limit"] = annotated
        };

        var ex = Assert.Throws<TargetInvocationException>(() => InvokeProcessVariableAnnotations(descriptor, dict));
        Assert.NotNull(ex.InnerException);
        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ProcessVariableAnnotations_SkipsVariablesWithNoAnnotations()
    {
        using var host = new KestrunHost("Tests", Logger);
        var descriptor = new OpenApiDocDescriptor(host, "test-doc");
        descriptor.Document.Components ??= new OpenApiComponents();
        descriptor.Document.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);

        var empty = new OpenApiComponentAnnotationScanner.AnnotatedVariable("ignored")
        {
            VariableType = typeof(string),
            InitialValue = "x",
            NoDefault = false
        };

        var dict = new Dictionary<string, OpenApiComponentAnnotationScanner.AnnotatedVariable>(StringComparer.Ordinal)
        {
            ["ignored"] = empty
        };

        InvokeProcessVariableAnnotations(descriptor, dict);

        Assert.NotNull(descriptor.Document.Components.Parameters);
        Assert.False(descriptor.Document.Components.Parameters.ContainsKey("ignored"));
    }
}
