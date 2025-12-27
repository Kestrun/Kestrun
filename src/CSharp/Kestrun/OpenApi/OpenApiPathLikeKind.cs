namespace Kestrun.OpenApi;
/// <summary>
/// Defines the kinds of OpenAPI path-like objects.
/// </summary>
public enum OpenApiPathLikeKind
{
    /// <summary>
    /// A standard OpenAPI path.
    /// </summary>
    Path,
    /// <summary>
    /// An OpenAPI webhook.
    /// </summary>
    Webhook,
    /// <summary>
    /// An OpenAPI callback.
    /// </summary>
    Callback
}
