using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Kestrun.Logging.Sinks;

/// <summary>
/// A Serilog sink that formats log events and invokes a callback for PowerShell integration.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PowerShellSink"/> class.
/// </remarks>
/// <param name="callback">The callback action invoked with the log event and its formatted message.</param>
/// <param name="outputTemplate">The output template used for formatting log messages.</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="callback"/> is null.</exception>
/// <remarks>
/// This constructor initializes the text formatter and callback action for the sink.
/// </remarks>
public class PowerShellSink(Action<LogEvent, string> callback, string outputTemplate = PowerShellSink.DEFAULT_OUTPUT_TEMPLATE) : ILogEventSink
{
    /// <summary>
    /// The default output template used for formatting log messages.
    /// </summary>
    public const string DEFAULT_OUTPUT_TEMPLATE = "{Message:lj}";

#if NET9_0_OR_GREATER
    private static readonly Lock _syncRoot = new();
#else
    private static readonly object _syncRoot = new();
#endif
    /// <summary>
    /// Gets or sets the text formatter used to format log events.
    /// </summary>
    public ITextFormatter TextFormatter { get; set; } = new MessageTemplateTextFormatter(string.IsNullOrWhiteSpace(outputTemplate) ? DEFAULT_OUTPUT_TEMPLATE : outputTemplate);

    /// <summary>
    /// Gets or sets the callback action that is invoked with the log event and its formatted message.
    /// </summary>
    public Action<LogEvent, string> Callback { get; set; } = callback ?? throw new ArgumentNullException(nameof(callback));

    /// <summary>
    /// Emits a log event by formatting it and invoking the callback action.
    /// </summary>
    /// <param name="logEvent">The log event to emit.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="logEvent"/> is null.</exception>
    /// <remarks>
    /// This method formats the log event using the specified text formatter and invokes the callback with the formatted message.
    /// </remarks>
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        using StringWriter strWriter = new();
        TextFormatter.Format(logEvent, strWriter);
        var renderedMessage = strWriter.ToString();

        lock (_syncRoot)
        {
            Callback(logEvent, renderedMessage);
        }
    }
}
