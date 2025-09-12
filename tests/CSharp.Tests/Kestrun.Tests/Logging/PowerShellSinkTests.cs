using Kestrun.Logging.Sinks;
using Kestrun.Logging.Sinks.Extensions;
using Kestrun.Logging.Utils.Console;
using Kestrun.Logging.Utils.Console.Extensions;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using System.Globalization;

namespace KestrunTests.Logging;

public class PowerShellSinkTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Emits_Formatted_Message_To_Callback()
    {
        // Arrange
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.PowerShell(Callback, outputTemplate: "[{Level:u3}] {Message:lj}")
            .CreateLogger();

        // Act
        logger.Information("Hello {Name}", "World");

        // Assert
        _ = Assert.Single(messages);
        Assert.Contains("[INF] Hello World", messages[0]);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_NullOrWhitespace_Template_Defaults()
    {
        // Arrange
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }
        var sink = new PowerShellSink(Callback, outputTemplate: " "); // whitespace -> default

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act
        logger.Information("Hi");

        // Assert: default template is just the message
        _ = Assert.Single(messages);
        Assert.Equal("Hi", messages[0].Trim());
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Throws_On_Null_Callback()
        => Assert.Throws<ArgumentNullException>(() => new PowerShellSink(null!));

    [Fact]
    [Trait("Category", "Logging")]
    public void TableExtensions_AddPropertyRow_Skips_Null_And_Empty_String_And_Clamps_Long()
    {
        var t = new Table(new Padding(0));

        // null -> skip
        t.AddPropertyRow("A", null!);

        // empty string -> skip
        t.AddPropertyRow("B", "");

        // long string -> clamp
        var longStr = new string('x', 10_000);
        t.AddPropertyRow("C", longStr);

        var rendered = t.RenderWithoutGrid();

        // Only one row should be present for C
        Assert.Contains("C", rendered);
        // clamped to 8192 with ellipsis
        Assert.Contains(new string('x', 8_192), rendered);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Explicit_Template_DirectSink_Renders_Level_Message_And_Property()
    {
        // Arrange
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }
        var template = "[{Level:u3}] {Message:lj} ({User})";
        var sink = new PowerShellSink(Callback, template);

        var parser = new MessageTemplateParser();
        var mt = parser.Parse("Hello");
        var props = new List<LogEventProperty>
        {
            new("User", new ScalarValue("alice"))
        };
        var evt = new LogEvent(DateTimeOffset.Now, LogEventLevel.Warning, null, mt, props);

        // Act
        sink.Emit(evt);

        // Assert
        _ = Assert.Single(messages);
        Assert.Equal("[WRN] Hello (alice)", messages[0].Trim());
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Explicit_Template_ViaLogger_Renders_Level_Message_And_Property()
    {
        // Arrange
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }
        var template = "[{Level:u3}] {Message:lj} ({User})";

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.PowerShell(Callback, outputTemplate: template)
            .CreateLogger();

        // Act
        logger.ForContext("User", "alice").Warning("Hello");

        // Assert
        _ = Assert.Single(messages);
        Assert.Equal("[WRN] Hello (alice)", messages[0].Trim());
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Explicit_Template_Handles_NonString_Scalar()
    {
        // Arrange
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }
        var template = "[{Level:u3}] {Message:lj} Age={Age}";

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.PowerShell(Callback, outputTemplate: template)
            .CreateLogger();

        // Act
        logger.ForContext("Age", 42).Information("Hi");

        // Assert
        _ = Assert.Single(messages);
        Assert.Equal("[INF] Hi Age=42", messages[0].Trim());
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Explicit_Template_Handles_Structured_Sequence()
    {
        // Arrange
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }
        var template = "{Message:lj} Items={Items}";
        var sink = new PowerShellSink(Callback, template);

        var parser = new MessageTemplateParser();
        var mt = parser.Parse("Hello");
        var seq = new SequenceValue([
            new ScalarValue(1),
            new ScalarValue(2),
            new ScalarValue(3),
        ]);
        var evt = new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            mt,
            [new("Items", seq)]);

        // Act
        sink.Emit(evt);

        // Assert
        _ = Assert.Single(messages);
        Assert.Equal("Hello Items=[1, 2, 3]", messages[0].Trim());
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Explicit_Template_Handles_Structured_Object_ViaLogger()
    {
        // Arrange
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }
        var template = "{Message:lj} User={User}";

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.PowerShell(Callback, outputTemplate: template)
            .CreateLogger();

        // Act
        logger.ForContext("User", new { Name = "alice", Age = 42 }, destructureObjects: true)
            .Information("Hello");

        // Assert
        _ = Assert.Single(messages);
        var rendered = messages[0].Trim();
        Assert.Contains("Hello User=", rendered);
        Assert.Contains("Name", rendered);
        Assert.Contains("\"alice\"", rendered);
        Assert.Contains("Age", rendered);
        Assert.Contains("42", rendered);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void PowerShellSink_Uses_InvariantCulture_For_Numeric_Formatting()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        var messages = new List<string>();
        void Callback(LogEvent evt, string rendered) { messages.Add(rendered); }
        var template = "{Value}";

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            using var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.PowerShell(Callback, outputTemplate: template)
                .CreateLogger();

            // Act
            logger.ForContext("Value", 12.34).Information(".");

            // Assert
            _ = Assert.Single(messages);
            Assert.Contains("12.34", messages[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
