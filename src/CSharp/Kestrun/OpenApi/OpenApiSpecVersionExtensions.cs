using Microsoft.OpenApi;

namespace Kestrun.OpenApi;
/// <summary>
/// Extension methods for OpenApiSpecVersion enum.
/// </summary>
public static class OpenApiSpecVersionExtensions
{
    /// <summary>
    /// Converts a string representation of an OpenAPI spec version to the corresponding enum value.
    /// </summary>
    /// <param name="version">The string representation of the OpenAPI spec version.</param>
    /// <returns>The corresponding <see cref="OpenApiSpecVersion"/> enum value.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided version string is not recognized.</exception>
    public static OpenApiSpecVersion ParseOpenApiSpecVersion(this string version)
    {
        var normalized = version.TrimStart('v', 'V');  // e.g. "2.0", "3.0", "3.1"
        return normalized switch
        {
            "2.0" => OpenApiSpecVersion.OpenApi2_0,
            "3.0" or "3.0.0" or "3.0.1" or "3.0.2" or "3.0.3" or "3.0.4" => OpenApiSpecVersion.OpenApi3_0,
            "3.1" or "3.1.0" or "3.1.1" => OpenApiSpecVersion.OpenApi3_1,
            "3.2" or "3.2.0" => OpenApiSpecVersion.OpenApi3_2,
            _ => throw new ArgumentException($"Unsupported OpenAPI spec version: {version}", nameof(version)),
        };
    }

    /// <summary>
    /// Converts an <see cref="OpenApiSpecVersion"/> enum value to its string representation.
    /// </summary>
    /// <param name="specVersion">The <see cref="OpenApiSpecVersion"/> enum value.</param>
    /// <returns>The string representation of the OpenAPI spec version.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the provided enum value is not recognized.</exception>
    public static string ToVersionString(this OpenApiSpecVersion specVersion)
    {
        return specVersion switch
        {
            OpenApiSpecVersion.OpenApi2_0 => "2.0",
            OpenApiSpecVersion.OpenApi3_0 => "3.0.4",
            OpenApiSpecVersion.OpenApi3_1 => "3.1.2",
            OpenApiSpecVersion.OpenApi3_2 => "3.2.0",
            _ => throw new ArgumentOutOfRangeException(nameof(specVersion), specVersion, null)
        };
    }
}
