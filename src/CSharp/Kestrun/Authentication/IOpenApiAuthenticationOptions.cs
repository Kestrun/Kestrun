namespace Kestrun.Authentication;

/// <summary>
/// Defines options for OpenAPI authentication schemes.
/// </summary>
public interface IOpenApiAuthenticationOptions
{
    /// <summary>
    /// If true, this security scheme is applied globally in OpenAPI documentation.
    /// </summary>
    bool GlobalScheme { get; set; }

    /// <summary>
    /// Optional description for the security scheme in OpenAPI documentation.
    /// </summary>
    string? Description { get; set; }

    /// <summary>
    /// Optional documentation identifiers associated with this authentication scheme.
    /// </summary>
    string[] DocumentationId { get; set; }


}
