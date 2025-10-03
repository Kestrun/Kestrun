using Kestrun.TBuilder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

namespace Kestrun.Hosting.Compression;

/// <summary>
/// A response compression provider that respects endpoint metadata to disable compression.
/// Wraps the built-in <see cref="ResponseCompressionProvider"/>.
/// </summary>
/// <remarks>
/// Creates a new <see cref="KestrunResponseCompressionProvider"/>.
/// </remarks>
/// <param name="services">The service provider.</param>
/// <param name="options">The response compression options.</param>
/// <param name="log">The logger.</param>
public sealed class KestrunResponseCompressionProvider(
    IServiceProvider services,
    IOptions<ResponseCompressionOptions> options,
    ILogger<KestrunResponseCompressionProvider> log) : IResponseCompressionProvider
{
    private readonly ResponseCompressionProvider _inner = ActivatorUtilities.CreateInstance<ResponseCompressionProvider>(services, options);
    private readonly ILogger<KestrunResponseCompressionProvider> _log = log;

    private static bool DisabledFor(HttpContext ctx) =>
        ctx.GetEndpoint()?.Metadata.Contains(EndpointDisablingCompressionExtensions.DisableResponseCompressionKey) == true;

    /// <summary>
    /// Determines if the request accepts compression, taking into account endpoint metadata.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>True if the request accepts compression; otherwise, false.</returns>
    public bool CheckRequestAcceptsCompression(HttpContext context)
        => !DisabledFor(context) && _inner.CheckRequestAcceptsCompression(context);

    /// <summary>
    /// Gets the compression provider for the given HTTP context, taking into account endpoint metadata.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The compression provider, or null if compression is disabled.</returns>
    public ICompressionProvider? GetCompressionProvider(HttpContext context)
        => !DisabledFor(context) ? _inner.GetCompressionProvider(context) : null;

    /// <summary>
    /// Determines if the response should be compressed, taking into account endpoint metadata.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>True if the response should be compressed; otherwise, false.</returns>
    public bool ShouldCompressResponse(HttpContext context)
        => !DisabledFor(context) && _inner.ShouldCompressResponse(context);
}
