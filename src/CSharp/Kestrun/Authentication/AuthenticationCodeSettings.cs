using System.Reflection;
using Kestrun.Scripting;

namespace Kestrun.Authentication;

/// <summary>
/// Represents the settings for authentication code, including language, code, extra imports, and references.
/// </summary>
public record AuthenticationCodeSettings
{
    /// <summary>
    /// Gets the scripting language used for authentication code.
    /// </summary>
    public ScriptLanguage Language { get; init; } = ScriptLanguage.Native;
    /// <summary>
    /// Gets the authentication code as a string.
    /// </summary>
    public string? Code { get; init; }
    /// <summary>
    /// Gets the extra import namespaces required for authentication code.
    /// </summary>
    public string[]? ExtraImports { get; init; }
    /// <summary>
    /// Gets the extra assembly references required for authentication code.
    /// </summary>
    public Assembly[]? ExtraRefs { get; init; }

    /// <summary>
    /// Gets the C# language version used for authentication code.
    /// If the language is CSharp.
    /// </summary>
    /// <remarks>
    /// This property is used to specify the C# language version for the authentication code.
    /// </remarks>
    public Microsoft.CodeAnalysis.CSharp.LanguageVersion CSharpVersion { get; init; } = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12;

    /// <summary>
    /// Gets the Visual Basic language version used for authentication code.
    /// If the language is VBNet.
    /// </summary>
    /// <remarks>
    /// This property is used to specify the Visual Basic language version for the authentication code.
    /// </remarks>
    public Microsoft.CodeAnalysis.VisualBasic.LanguageVersion VisualBasicVersion { get; init; } = Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic12;
}
