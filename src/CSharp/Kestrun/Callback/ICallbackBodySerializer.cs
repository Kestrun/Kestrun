namespace Kestrun.Callback;

/// <summary>
/// Serializes the body of a callback request based on the callback plan and runtime context.
/// </summary>
public interface ICallbackBodySerializer
{
    /// <summary>
    /// Serializes the callback body based on the provided plan and context.
    /// </summary>
    /// <param name="plan">The callback plan containing the body definition.</param>
    /// <param name="ctx">The callback runtime context providing the payload.</param>
    /// <returns>A tuple containing the content type and serialized body bytes.</returns>
    (string ContentType, byte[] Body) Serialize(CallbackPlan plan, CallbackRuntimeContext ctx);
}
