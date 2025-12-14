using Microsoft.OpenApi;

namespace Kestrun.Hosting.Options;

/// <summary>
/// Metadata for OpenAPI documentation related to the route.
/// </summary>
public record OpenAPICommonMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAPICommonMetadata"/> class with the specified pattern.
    /// </summary>
    /// <param name="pattern">The route pattern.</param>
    public OpenAPICommonMetadata(string pattern)
    {
        Pattern = pattern;
        Enabled = true;
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAPICommonMetadata"/> class.
    /// </summary>
    public OpenAPICommonMetadata()
    {
        Pattern = "/";
        Enabled = true;
    }
    /// <summary>
    /// Indicates whether OpenAPI documentation is enabled for this route.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The relative path for the route in OpenAPI documentation.
    /// </summary>
    public string Pattern { get; set; }

    /// <summary>
    /// A brief summary of the route for OpenAPI documentation.
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// A detailed description of the route for OpenAPI documentation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The name of the CORS policy to apply to this route.
    /// </summary>
    public string? CorsPolicy { get; set; }

    /// <summary>
    /// An alternative server array to service this operation.
    /// If an alternative server object is specified at the Path Item Object or Root level,
    /// it will be overridden by this value.
    /// </summary>
    public IList<OpenApiServer>? Servers { get; set; } = [];

    /// <summary>
    /// A list of parameters that are applicable for this operation.
    /// If a parameter is already defined at the Path Item, the new definition will override it but can never remove it.
    /// The list MUST NOT include duplicated parameters. A unique parameter is defined by a combination of a name and location.
    /// The list can use the Reference Object to link to parameters that are defined at the OpenAPI Object's components/parameters.
    /// </summary>
    public IList<IOpenApiParameter>? Parameters { get; set; } = [];
}
