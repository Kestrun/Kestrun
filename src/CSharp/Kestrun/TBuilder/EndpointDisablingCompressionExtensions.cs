namespace Kestrun.TBuilder;

/// <summary>
/// Extension methods for <see cref="IEndpointConventionBuilder"/> to support disabling compression metadata.
/// </summary>
public static class EndpointDisablingCompressionExtensions
{
    /// <summary>
    /// Metadata key to indicate that compression should be disabled for this endpoint.
    /// Used by compression middleware to skip compression for this endpoint.
    /// </summary>
    public static readonly object DisableResponseCompressionKey = new();

    /// <summary>
    ///  Metadata key to indicate that compression should be disabled for this endpoint.
    ///  Used by compression middleware to skip compression for this endpoint.
/// </summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The endpoint convention builder, for fluent chaining.</returns>
    public static TBuilder DisableResponseCompression<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(e => e.Metadata.Add(DisableResponseCompressionKey));
        return builder;
    }
}
