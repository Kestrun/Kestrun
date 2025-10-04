
using System.Reflection;
using Kestrun.Scripting;

namespace Kestrun.Hosting.Options;
/// <summary>
/// Base options for specifying script code and language settings.
/// </summary>
public record LanguageOptions
{
    /// <summary>
    /// The script code to execute for this route.
    /// </summary>
    public string? Code { get; set; }
    /// <summary>
    /// The scripting language used for the route's code.
    /// </summary>
    public ScriptLanguage Language { get; set; } = ScriptLanguage.PowerShell;
    /// <summary>
    /// Additional import namespaces required for the script code.
    /// </summary>
    public string[]? ExtraImports { get; set; }
    /// <summary>
    /// Additional assembly references required for the script code.
    /// </summary>
    public Assembly[]? ExtraRefs { get; set; }
}
