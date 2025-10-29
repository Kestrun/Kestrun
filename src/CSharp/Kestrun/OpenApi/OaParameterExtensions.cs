using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Extensions for <see cref="OaParameterLocation"/>.
/// </summary>
public static class OaParameterExtensions
{
    /// <summary>Convert to Microsoft.OpenApi ParameterLocation.</summary>
    /// <param name="location">The OaParameterLocation value.</param>
    /// <returns>The corresponding ParameterLocation value.</returns>
    public static ParameterLocation ToOpenApi(this OaParameterLocation location)
        => location switch
        {
            OaParameterLocation.Query => ParameterLocation.Query,
            OaParameterLocation.Header => ParameterLocation.Header,
            OaParameterLocation.Path => ParameterLocation.Path,
            OaParameterLocation.Cookie => ParameterLocation.Cookie,
            _ => throw new ArgumentOutOfRangeException(nameof(location), location, null)
        };

    /// <summary>
    /// Convert to Microsoft.OpenApi ParameterStyle.
    /// </summary>
    /// <param name="style"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static ParameterStyle ToOpenApi(this OaParameterStyle style)
        => style switch
        {
            OaParameterStyle.Simple => ParameterStyle.Simple,
            OaParameterStyle.Form => ParameterStyle.Form,
            OaParameterStyle.Matrix => ParameterStyle.Matrix,
            OaParameterStyle.Label => ParameterStyle.Label,
            OaParameterStyle.SpaceDelimited => ParameterStyle.SpaceDelimited,
            OaParameterStyle.PipeDelimited => ParameterStyle.PipeDelimited,
            OaParameterStyle.DeepObject => ParameterStyle.DeepObject,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
        };

    /// <summary>
    /// Convert to Microsoft.OpenApi ParameterLocation.
    /// </summary>
    /// <param name="location">The OaParameterLocation value.</param>
    /// <returns>The corresponding ParameterLocation value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the location is not recognized.</exception>
    public static ParameterLocation ToParameterLocation(this OaParameterLocation location)
    {
        return location switch
        {
            OaParameterLocation.Query => ParameterLocation.Query,
            OaParameterLocation.Header => ParameterLocation.Header,
            OaParameterLocation.Path => ParameterLocation.Path,
            OaParameterLocation.Cookie => ParameterLocation.Cookie,
            _ => throw new ArgumentOutOfRangeException(nameof(location), location, null)
        };
    }

    /// <summary>
    /// Convert to Microsoft.OpenApi ParameterStyle.
    /// </summary>
    /// <param name="style">The OaParameterStyle value.</param>
    /// <returns>The corresponding ParameterStyle value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the style is not recognized.</exception>
    public static ParameterStyle ToParameterStyle(this OaParameterStyle? style)
    {
        return style switch
        {
            OaParameterStyle.Simple => ParameterStyle.Simple,
            OaParameterStyle.Form => ParameterStyle.Form,
            OaParameterStyle.Matrix => ParameterStyle.Matrix,
            OaParameterStyle.Label => ParameterStyle.Label,
            OaParameterStyle.SpaceDelimited => ParameterStyle.SpaceDelimited,
            OaParameterStyle.PipeDelimited => ParameterStyle.PipeDelimited,
            OaParameterStyle.DeepObject => ParameterStyle.DeepObject,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
        };
    }
}
