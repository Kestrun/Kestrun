namespace Kestrun.Scripting;

/// <summary>
/// Extension methods for <see cref="IEndpointConventionBuilder"/> to support script language metadata.
/// </summary>
public static class EndpointConventionBuilderLanguageExtensions
{
    /// <summary>
    /// Tags any endpoint builder with <see cref="ScriptLanguageAttribute"/>.
    /// Works for RouteHandlerBuilder, RouteGroupBuilder, etc.
    /// </summary>
    public static TBuilder WithLanguage<TBuilder>(
        this TBuilder builder, ScriptLanguage language)
        where TBuilder : IEndpointConventionBuilder
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        // IEndpointConventionBuilder exposes Add(Action<EndpointBuilder>)
        builder.Add(ep => ep.Metadata.Add(new ScriptLanguageAttribute(language)));
        return builder;   // keep fluent chaining
    }
}
