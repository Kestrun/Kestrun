namespace Kestrun.Localization;

/// <summary>
/// Options for configuring PowerShell-style localization using string table files.
/// </summary>
public sealed class KestrunLocalizationOptions
{
    /// <summary>
    /// Gets or sets the default culture to use when no match is found. Default is "en-US".
    /// </summary>
    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the base path for localization resources. Default is "i18n".
    /// </summary>
    public string ResourcesBasePath { get; set; } = "i18n";

    /// <summary>
    /// Gets or sets the localization file name. Default is "Messages.psd1".
    /// </summary>
    public string FileName { get; set; } = "Messages.psd1";

    /// <summary>
    /// Gets or sets the query string key used to request a culture. Default is "lang".
    /// </summary>
    public string QueryKey { get; set; } = "lang";

    /// <summary>
    /// Gets or sets the cookie name used to request a culture. Default is "lang".
    /// </summary>
    public string CookieName { get; set; } = "lang";

    /// <summary>
    /// Gets or sets a value indicating whether Accept-Language should be used. Default is true.
    /// </summary>
    public bool EnableAcceptLanguage { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether query string resolution is enabled. Default is true.
    /// </summary>
    public bool EnableQuery { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether cookie resolution is enabled. Default is true.
    /// </summary>
    public bool EnableCookie { get; set; } = true;
}
