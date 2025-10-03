using Microsoft.AspNetCore.ResponseCompression;

namespace Kestrun.Hosting.Compression;

/// <summary>
/// Extension methods to add Kestrun compression services.
/// </summary>
public static class KestrunCompressionServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kestrun compression services to the service collection.
    /// This replaces the default <see cref="IResponseCompressionProvider"/> with
    /// <see cref="KestrunResponseCompressionProvider"/>, which respects endpoint metadata to
    /// disable compression.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKestrunCompressionOptOut(this IServiceCollection services) =>
        // Replace the default provider with our decorator
        services.AddSingleton<IResponseCompressionProvider, KestrunResponseCompressionProvider>();
}
