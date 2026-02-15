namespace Kestrun.Hosting.Options;

/// <summary>
/// Represents a response content type and its associated schema for this route.
/// </summary>
/// <param name="ContentType">The response content type.</param>
/// <param name="Schema">The schema type associated with the response content type.</param>
public record ContentTypeWithSchema(string ContentType, string? Schema = null);
