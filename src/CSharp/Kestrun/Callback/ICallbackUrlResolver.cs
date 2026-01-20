namespace Kestrun.Callback;

/// <summary>
/// Resolves callback URLs based on templates and runtime context.
/// </summary>
public interface ICallbackUrlResolver
{
    /// <summary>
    /// Resolves the given URL template using the provided callback runtime context.
    /// </summary>
    /// <param name="urlTemplate">The URL template containing tokens and JSON Pointer expressions.</param>
    /// <param name="ctx">The callback runtime context providing values for tokens and JSON data.</param>
    /// <returns>A fully resolved URI based on the template and context.</returns>
    Uri Resolve(string urlTemplate, CallbackRuntimeContext ctx);
}
