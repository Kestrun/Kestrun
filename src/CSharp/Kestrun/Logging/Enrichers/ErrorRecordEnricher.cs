using Kestrun.Logging.Exceptions;
using Serilog.Core;
using Serilog.Events;

namespace Kestrun.Logging.Enrichers;

/// <summary>
/// Enriches Serilog log events with error record and invocation info from WrapperException.
/// </summary>
public class ErrorRecordEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name used for the error record in log events.
    /// </summary>
    public const string ERR_PROPERTY_NAME_FULL = "ErrorRecord";
    /// <summary>
    /// The property name used for the invocation info in log events.
    /// </summary>
    public const string II_PROPERTY_NAME_FULL = "InvocationInfo";

    /// <summary>
    /// Gets a value indicating whether objects should be destructured when enriching log events.
    /// </summary>
    public bool DestructureObjects { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorRecordEnricher"/> class.
    /// </summary>
    public ErrorRecordEnricher()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorRecordEnricher"/> class with the option to destructure objects.
    /// </summary>
    /// <param name="destructureObjects">Indicates whether objects should be destructured when enriching log events.</param>
    public ErrorRecordEnricher(bool destructureObjects) => DestructureObjects = destructureObjects;

    /// <summary>
    /// Enriches the log event with error record and invocation info properties if the exception is a <see cref="WrapperException"/>.
    /// </summary>
    /// <param name="logEvent">The log event to enrich.</param>
    /// <param name="propertyFactory">The property factory used to create log event properties.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Exception is WrapperException wrapperException)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ERR_PROPERTY_NAME_FULL, wrapperException.ErrorRecordWrapper, DestructureObjects));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                II_PROPERTY_NAME_FULL,
                wrapperException.ErrorRecordWrapper?.InvocationInfoWrapper,
                DestructureObjects));
        }
    }
}
