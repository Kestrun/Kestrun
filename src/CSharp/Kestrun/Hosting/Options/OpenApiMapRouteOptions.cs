using Microsoft.OpenApi;

namespace Kestrun.Hosting.Options;

/// <summary>
/// Options for OpenAPI map route.
/// </summary>
public record OpenApiMapRouteOptions
{
    /// <summary>
    /// Constructor for OpenApiMapRouteOptions.
    /// </summary>
    /// <param name="mapOptions"></param>
    public OpenApiMapRouteOptions(MapRouteOptions mapOptions)
    {
        MapOptions = mapOptions;
        if (MapOptions.Pattern == null)
        {
            MapOptions.Pattern = "/openapi/{version:regex(^v(2\\.0|3\\.0(\\.\\d+)?|3\\.1(\\.\\d+)?$)?)}/{format:regex(^(json|yaml)$)?}";
        }
    }
    /// <summary>
    /// The document ID to serve.
    /// </summary>
    public string DocId { get; set; } = Authentication.IOpenApiAuthenticationOptions.DefaultSchemeName;
    /// <summary>
    /// The supported OpenAPI spec versions.
    /// </summary>
    public OpenApiSpecVersion[] SpecVersion { get; set; } =
      [OpenApiSpecVersion.OpenApi2_0, OpenApiSpecVersion.OpenApi3_0, OpenApiSpecVersion.OpenApi3_1];
    /// <summary>
    /// The name of the route variable for version.
    /// </summary>
    public string VersionVarName { get; set; } = "version";
    /// <summary>
    /// The name of the route variable for format.
    /// </summary>
    public string FormatVarName { get; set; } = "format";
    /// <summary>
    /// The name of the query variable for refresh.
    /// </summary>
    public string RefreshVarName { get; set; } = "refresh";
    /// <summary>
    /// The default format to use if not specified in the route.
    /// </summary>
    public string DefaultFormat { get; set; } = "json";
    /// <summary>
    /// The default version to use if not specified in the route.
    /// </summary>
    public string DefaultVersion { get; set; } = "v3.0";
    /// <summary>
    /// The map route options.
    /// </summary>
    public MapRouteOptions MapOptions { get; set; }
}
