using System.Text.Json;

namespace Kestrun.Callback;

/// <summary>
/// JSON implementation of <see cref="ICallbackBodySerializer"/>.
/// </summary>
public sealed class JsonCallbackBodySerializer : ICallbackBodySerializer
{
    /// <summary>
    /// Serializes the callback body based on the provided plan and context.
    /// </summary>
    /// <param name="plan">The callback plan containing the body definition.</param>
    /// <param name="ctx">The callback runtime context providing the payload.</param>
    /// <returns>A tuple containing the content type and serialized body bytes.</returns>
    public (string ContentType, byte[] Body) Serialize(CallbackPlan plan, CallbackRuntimeContext ctx)
    {
        // If no body defined in plan → send empty
        if (plan.Body is null)
        {
            return ("application/json", Array.Empty<byte>());
        }

        var ct = plan.Body.MediaType ?? "application/json";

        // Your payload must be provided by handler earlier
        if (ctx.CallbackPayload is null)
        {
            return (ct, Array.Empty<byte>());
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(ctx.CallbackPayload);
        return (ct, bytes);
    }
}
