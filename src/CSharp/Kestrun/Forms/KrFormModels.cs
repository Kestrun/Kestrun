using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Kestrun.Forms;

/// <summary>
/// Represents the abstract base for all form payload types.
/// </summary>
public abstract record KrFormPayload
{
    /// <summary>
    /// Gets the type of the form payload.
    /// </summary>
    public abstract KrFormPayloadType PayloadType { get; }
}

/// <summary>
/// Specifies the type of form payload.
/// </summary>
public enum KrFormPayloadType
{
    /// <summary>
    /// Named parts with fields and files (multipart/form-data, application/x-www-form-urlencoded).
    /// </summary>
    NamedParts,

    /// <summary>
    /// Ordered parts preserving sequence (multipart/mixed and other multipart/* types).
    /// </summary>
    OrderedParts
}

/// <summary>
/// Represents a form payload with named parts (fields and files).
/// Used for multipart/form-data and application/x-www-form-urlencoded.
/// </summary>
public sealed record KrNamedPartsPayload : KrFormPayload
{
    /// <summary>
    /// Gets the payload type.
    /// </summary>
    public override KrFormPayloadType PayloadType => KrFormPayloadType.NamedParts;

    /// <summary>
    /// Gets the text fields dictionary. Key is field name, value is array of field values.
    /// </summary>
    public Dictionary<string, string[]> Fields { get; init; } = new();

    /// <summary>
    /// Gets the files dictionary. Key is field name, value is array of file parts.
    /// </summary>
    public Dictionary<string, KrFilePart[]> Files { get; init; } = new();
}

/// <summary>
/// Represents a form payload with ordered parts preserving sequence.
/// Used for multipart/mixed and other multipart/* types (non-form-data).
/// </summary>
public sealed record KrOrderedPartsPayload : KrFormPayload
{
    /// <summary>
    /// Gets the payload type.
    /// </summary>
    public override KrFormPayloadType PayloadType => KrFormPayloadType.OrderedParts;

    /// <summary>
    /// Gets the ordered list of raw parts.
    /// </summary>
    public List<KrRawPart> Parts { get; init; } = new();
}

/// <summary>
/// Represents a file part from a multipart/form-data request.
/// </summary>
public sealed record KrFilePart
{
    /// <summary>
    /// Gets the field name from Content-Disposition.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the original filename from Content-Disposition.
    /// </summary>
    public string? OriginalFileName { get; init; }

    /// <summary>
    /// Gets the content type of the file part.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the length in bytes of the stored file.
    /// </summary>
    public long Length { get; init; }

    /// <summary>
    /// Gets the temporary file path where the part was stored.
    /// </summary>
    public required string TempPath { get; init; }

    /// <summary>
    /// Gets the SHA-256 hash of the file content (if computed).
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Gets the headers associated with this part.
    /// </summary>
    public Dictionary<string, StringValues>? Headers { get; init; }
}

/// <summary>
/// Represents a raw part from multipart/mixed or other ordered multipart types.
/// </summary>
public sealed record KrRawPart
{
    /// <summary>
    /// Gets the name from Content-Disposition (optional for ordered parts).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the content type of the part.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the length in bytes of the stored part.
    /// </summary>
    public long Length { get; init; }

    /// <summary>
    /// Gets the temporary file path where the part was stored.
    /// </summary>
    public required string TempPath { get; init; }

    /// <summary>
    /// Gets the headers associated with this part.
    /// </summary>
    public Dictionary<string, StringValues>? Headers { get; init; }

    /// <summary>
    /// Gets the nested payload if this part contains a nested multipart/* section.
    /// </summary>
    public KrFormPayload? NestedPayload { get; init; }
}

/// <summary>
/// Represents the context for form parsing operations.
/// </summary>
public sealed record KrFormContext
{
    /// <summary>
    /// Gets the HTTP context.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>
    /// Gets the form options used for parsing.
    /// </summary>
    public required KrFormOptions Options { get; init; }

    /// <summary>
    /// Gets the parsed form payload.
    /// </summary>
    public required KrFormPayload Payload { get; init; }

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    public required Serilog.ILogger Logger { get; init; }
}

/// <summary>
/// Represents the context for processing a single part.
/// </summary>
public sealed record KrPartContext
{
    /// <summary>
    /// Gets the form context.
    /// </summary>
    public required KrFormContext FormContext { get; init; }

    /// <summary>
    /// Gets the zero-based index of the part.
    /// </summary>
    public required int PartIndex { get; init; }

    /// <summary>
    /// Gets the part name (if available).
    /// </summary>
    public string? PartName { get; init; }

    /// <summary>
    /// Gets the original filename (if available).
    /// </summary>
    public string? OriginalFileName { get; init; }

    /// <summary>
    /// Gets the part content type.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the part headers.
    /// </summary>
    public Dictionary<string, StringValues>? Headers { get; init; }
}

/// <summary>
/// Specifies the action to take for a part.
/// </summary>
public enum KrPartAction
{
    /// <summary>
    /// Continue processing the part normally.
    /// </summary>
    Continue,

    /// <summary>
    /// Skip the part without storing it.
    /// </summary>
    Skip,

    /// <summary>
    /// Reject the entire request.
    /// </summary>
    Reject
}

/// <summary>
/// Represents a sink for streaming part data to storage.
/// </summary>
public interface IKrPartSink
{
    /// <summary>
    /// Writes data to the sink asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer containing data to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Completes the write operation and returns the result.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the temp path, length in bytes, and optional SHA-256 hash.</returns>
    ValueTask<(string TempPath, long Length, string? Sha256)> CompleteAsync(CancellationToken cancellationToken);
}
