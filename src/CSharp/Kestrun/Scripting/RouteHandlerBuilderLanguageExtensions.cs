namespace Kestrun.Scripting;

/// <summary>
/// Extension methods for <see cref="RouteHandlerBuilder"/> to support script language metadata.
/// </summary>
public static class RouteHandlerBuilderLanguageExtensions
{
    /// <summary>
    /// Tags a <see cref="RouteHandlerBuilder"/> with a <see cref="ScriptLanguageAttribute"/> for the specified language.
    /// </summary>
    /// <param name="b">The route handler builder to tag.</param>
    /// <param name="lang">The script language to associate with the route handler.</param>
    /// <returns>The same <see cref="RouteHandlerBuilder"/> instance for fluent chaining.</returns>
    public static RouteHandlerBuilder WithLanguage(
             this RouteHandlerBuilder b, ScriptLanguage lang) =>
         b.WithMetadata(new ScriptLanguageAttribute(lang));
}
