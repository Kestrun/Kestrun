using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;


namespace Kestrun.Logging.Sinks.Extensions;

/// <summary>
/// Provides extension methods for configuring PowerShell sinks in Serilog logging.
/// </summary>
public static class PowerShellSinkExtensions
{
    /// <summary>
    /// Adds a PowerShell sink to the Serilog logger configuration.
    /// </summary>
    /// <param name="loggerConfiguration">The logger sink configuration.</param>
    /// <param name="callback">Callback to handle log events and formatted output.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink.</param>
    /// <param name="outputTemplate">The output template for formatting log messages.</param>
    /// <param name="levelSwitch">Optional level switch for controlling logging level.</param>
    /// <returns>The logger configuration with the PowerShell sink added.</returns>
    public static LoggerConfiguration PowerShell(this LoggerSinkConfiguration loggerConfiguration,
        Action<LogEvent, string> callback,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        string outputTemplate = PowerShellSink.DEFAULT_OUTPUT_TEMPLATE,
        LoggingLevelSwitch? levelSwitch = null) => loggerConfiguration.Sink(new PowerShellSink(callback, outputTemplate), restrictedToMinimumLevel, levelSwitch);
}
