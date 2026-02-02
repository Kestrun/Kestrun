using System.Collections.ObjectModel;

namespace Kestrun.Forms;

/// <summary>
/// Represents a parsed form payload.
/// </summary>
public interface IKrFormPayload { }

/// <summary>
/// Represents a form payload containing named fields and files.
/// </summary>
[OpenApiSchemaComponent(
    Description = "Base schema for parsed multipart/form-data payloads. Concrete form models should declare the expected parts as properties.",
    Type = OaSchemaType.Object,
    AdditionalPropertiesAllowed = true
)]
public class KrFormData : IKrFormPayload
{
    /// <summary>
    /// Gets the parsed fields keyed by field name.
    /// </summary>
    public Dictionary<string, string[]> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the parsed files keyed by field name.
    /// </summary>
    public Dictionary<string, KrFilePart[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents a form payload containing ordered parts.
/// </summary>
[OpenApiSchemaComponent(
    Description = "Base schema for parsed multipart/mixed payloads. Concrete models should declare the expected parts as properties.",
    Type = OaSchemaType.Object,
    AdditionalPropertiesAllowed = true
)]
public class KrMultipart : IKrFormPayload
{
    /// <summary>
    /// Gets the ordered list of parts in the payload.
    /// </summary>
    public List<KrRawPart> Parts { get; } = [];
}

/// <summary>
/// Represents a stored file part.
/// </summary>
public sealed record KrFilePart
{
    /// <summary>
    /// Gets the field name associated with the file part.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the original file name.
    /// </summary>
    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Gets the content type of the file.
    /// </summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>
    /// Gets the length of the stored file.
    /// </summary>
    public long Length { get; init; }

    /// <summary>
    /// Gets the temporary storage path for the file.
    /// </summary>
    public required string TempPath { get; init; }

    /// <summary>
    /// Gets the SHA-256 hash of the file if computed.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Gets the headers associated with the part.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; init; } = new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Represents a stored raw part, preserving order in multipart/mixed payloads.
/// </summary>
public sealed record KrRawPart
{
    /// <summary>
    /// Gets the optional part name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the content type of the part.
    /// </summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>
    /// Gets the length of the stored part.
    /// </summary>
    public long Length { get; init; }

    /// <summary>
    /// Gets the temporary storage path for the part.
    /// </summary>
    public required string TempPath { get; init; }

    /// <summary>
    /// Gets the headers associated with the part.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; init; } = new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the nested multipart payload, if present.
    /// </summary>
    public IKrFormPayload? NestedPayload { get; init; }
}
