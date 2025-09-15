namespace Kestrun.Scripting;


/// <summary>
/// Specifies the supported scripting languages for script execution.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><term>Native</term><description>No scripting; executes native C# code.</description></item>
///   <item><term>PowerShell</term><description>Uses PowerShell scripting language.</description></item>
///   <item><term>CSharp</term><description>Uses C# scripting language.</description></item>
///   <item><term>FSharp</term><description>Uses F# scripting language.</description></item>
///   <item><term>Python</term><description>Uses Python scripting language.</description></item>
///   <item><term>JavaScript</term><description>Uses JavaScript scripting language (optional, e.g., via ClearScript/Jint).</description></item>
/// </list>
/// </remarks>
public enum ScriptLanguage
{
    /// <summary>
    /// No scripting; executes native C# code.
    /// </summary>
    Native,
    /// <summary>
    /// Uses PowerShell scripting language.
    /// </summary>
    PowerShell,
    /// <summary>
    /// Uses C# scripting language.
    /// </summary>
    CSharp,
    /// <summary>
    /// Uses F# scripting language.
    /// </summary>
    FSharp,
    /// <summary>
    /// Uses Python scripting language.
    /// </summary>
    Python,
    /// <summary>
    /// Uses JavaScript scripting language (optional, e.g., via ClearScript/Jint).
    /// </summary>
    JavaScript,
    /// <summary>
    /// Uses VB.NET scripting language.
    /// </summary>
    VBNet
}
