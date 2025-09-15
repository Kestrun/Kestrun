namespace Kestrun.Scripting;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to support language-based runtime configuration.
/// </summary>
public static class LanguageRuntimeExtensions
{
    /// <summary>
    /// Configures the application pipeline to use a specific language runtime for endpoints tagged with the given <see cref="ScriptLanguage"/>.
    /// </summary>
    /// <param name="app">The application builder to configure.</param>
    /// <param name="language">The script language to filter endpoints by.</param>
    /// <param name="configure">The configuration action to apply when the language matches.</param>
    /// <returns>The configured <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseLanguageRuntime(
        this IApplicationBuilder app,
        ScriptLanguage language,
        Action<IApplicationBuilder> configure)
    {
        return app.UseWhen(context =>
        {
            var attr =
                context.GetEndpoint()?.Metadata
                   .GetMetadata<ScriptLanguageAttribute>();
            return attr?.Language == language;
        }, configure);
    }
}
