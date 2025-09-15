namespace Kestrun.Scripting;

/// <summary>
/// Attribute to specify the script language for a method or delegate.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ScriptLanguageAttribute"/> class.
/// </remarks>
/// <param name="lang">The script language to associate with the method or delegate.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate,
             AllowMultiple = false)]
public sealed class ScriptLanguageAttribute(ScriptLanguage lang) : Attribute
{
    /// <summary>
    /// Gets the script language associated with this attribute.
    /// </summary>
    public ScriptLanguage Language { get; } = lang;
}

