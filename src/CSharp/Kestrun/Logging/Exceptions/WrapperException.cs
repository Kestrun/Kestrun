using System.Management.Automation;
using Kestrun.Logging.Data;

namespace Kestrun.Logging.Exceptions;

/// <summary>
/// Represents an exception that wraps another exception and optionally an ErrorRecord.
/// </summary>
public class WrapperException : Exception
{
    /// <summary>
    /// Gets the wrapped <see cref="ErrorRecordWrapper"/> associated with this exception, if any.
    /// </summary>
    public ErrorRecordWrapper? ErrorRecordWrapper { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WrapperException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public WrapperException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WrapperException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public WrapperException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WrapperException"/> class with a specified inner exception and an <see cref="ErrorRecord"/>.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="errorRecord">The <see cref="ErrorRecord"/> associated with this exception.</param>
    public WrapperException(Exception innerException, ErrorRecord errorRecord) : base(string.Empty, innerException) => ErrorRecordWrapper = new ErrorRecordWrapper(errorRecord);

    /// <summary>
    /// Initializes a new instance of the <see cref="WrapperException"/> class.
    /// </summary>
    public WrapperException()
    {
    }

    /// <summary>
    /// Returns a string representation of the inner exception, or an empty string if none exists.
    /// </summary>
    public override string ToString() => InnerException?.ToString() ?? string.Empty;
}
