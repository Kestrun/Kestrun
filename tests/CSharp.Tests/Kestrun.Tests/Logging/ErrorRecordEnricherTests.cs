using System.Management.Automation;
using Kestrun.Logging.Enrichers;
using Kestrun.Logging.Enrichers.Extensions;
using Kestrun.Logging.Exceptions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Logging;

public class ErrorRecordEnricherTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void WithErrorRecord_enriches_properties_when_Exception_is_WrapperException_destructured()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'boom' -Category InvalidOperation -ErrorId ERR456");
        _ = ps.Invoke();
        Assert.True(ps.HadErrors);
        var err = ps.Streams.Error[0];
        var ex = new WrapperException(new InvalidOperationException("inner!"), err);

        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithErrorRecord(desctructureObjects: true)
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act
        logger.Error(ex, "failure");

        // Assert
        var evt = sink.Events.LastOrDefault();
        Assert.NotNull(evt);
        Assert.True(evt!.Properties.ContainsKey(ErrorRecordEnricher.ERR_PROPERTY_NAME_FULL));
        Assert.True(evt.Properties.ContainsKey(ErrorRecordEnricher.II_PROPERTY_NAME_FULL));

        // When destructured, properties should be a StructureValue
        _ = Assert.IsType<StructureValue>(evt.Properties[ErrorRecordEnricher.ERR_PROPERTY_NAME_FULL]);
        _ = Assert.IsType<StructureValue>(evt.Properties[ErrorRecordEnricher.II_PROPERTY_NAME_FULL]);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void WithErrorRecord_does_nothing_for_non_wrapper_exception()
    {
        // Arrange
        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithErrorRecord()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act
        logger.Error(new InvalidOperationException("no wrapper"), "failure");

        // Assert
        var evt = sink.Events.LastOrDefault();
        Assert.NotNull(evt);
        Assert.False(evt!.Properties.ContainsKey(ErrorRecordEnricher.ERR_PROPERTY_NAME_FULL));
        Assert.False(evt.Properties.ContainsKey(ErrorRecordEnricher.II_PROPERTY_NAME_FULL));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void WithErrorRecord_enriches_properties_when_Exception_is_WrapperException_scalar()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'boom' -Category InvalidOperation -ErrorId ERR789");
        _ = ps.Invoke();
        Assert.True(ps.HadErrors);
        var err = ps.Streams.Error[0];
        var ex = new WrapperException(new InvalidOperationException("inner!"), err);

        var sink = new InMemorySink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            // default is desctructureObjects: false (scalar path)
            .Enrich.WithErrorRecord()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act
        logger.Error(ex, "failure");

        // Assert
        var evt = sink.Events.LastOrDefault();
        Assert.NotNull(evt);
        Assert.True(evt!.Properties.ContainsKey(ErrorRecordEnricher.ERR_PROPERTY_NAME_FULL));
        Assert.True(evt.Properties.ContainsKey(ErrorRecordEnricher.II_PROPERTY_NAME_FULL));

        // In scalar mode, values should be stringified (ScalarValue of string)
        var errProp = Assert.IsType<ScalarValue>(evt.Properties[ErrorRecordEnricher.ERR_PROPERTY_NAME_FULL]);
        var iiProp = Assert.IsType<ScalarValue>(evt.Properties[ErrorRecordEnricher.II_PROPERTY_NAME_FULL]);
        _ = Assert.IsType<string>(errProp.Value);
        _ = Assert.IsType<string>(iiProp.Value);
        Assert.False(string.IsNullOrEmpty((string)errProp.Value!));
        Assert.False(string.IsNullOrEmpty((string)iiProp.Value!));
    }

    private sealed class InMemorySink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
