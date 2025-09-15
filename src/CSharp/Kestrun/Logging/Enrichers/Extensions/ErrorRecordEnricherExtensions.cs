using Serilog;
using Serilog.Configuration;

namespace Kestrun.Logging.Enrichers.Extensions;

/// <summary>
/// Provides extension methods for enriching Serilog logs with error record information.
/// </summary>
public static class ErrorRecordEnricherExtensions
{
    /// <summary>
    /// Enriches Serilog logs with error record information.
    /// </summary>
    /// <param name="loggerConfiguration">The logger enrichment configuration.</param>
    /// <param name="desctructureObjects">Specifies whether to destructure objects in the error record.</param>
    /// <returns>The logger configuration with error record enrichment.</returns>
    public static LoggerConfiguration WithErrorRecord(this LoggerEnrichmentConfiguration loggerConfiguration, bool desctructureObjects = false) => loggerConfiguration.With(new ErrorRecordEnricher(desctructureObjects));
}
