
using System.Management.Automation;
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

    /// <summary>
    /// The script block created from the <see cref="Code"/> property, or null if no code is set.
    /// </summary>
    public ScriptBlock? ScriptBlock
    {
        get
        {
            return string.IsNullOrWhiteSpace(Code) ? null : ScriptBlock.Create(Code);
        }
        set
        {
            if (value is null)
            {
                Code = null;
                return;
            }
            Code = value.ToString();
            Language = ScriptLanguage.PowerShell;
        }
    }

    /// <summary>
    /// Additional metadata for the route, represented as key-value pairs.
    /// </summary>
    public Dictionary<string, object?>? Arguments { get; set; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
