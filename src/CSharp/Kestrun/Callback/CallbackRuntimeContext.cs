namespace Kestrun.Callback;

/// <summary>
/// Represents the runtime context for executing a callback operation.
/// </summary>
/// <param name="CorrelationId">The unique identifier for correlating the callback operation.</param>
/// <param name="IdempotencyKeySeed">The seed value used for generating idempotency keys to ensure idempotent callback execution.</param>
/// <param name="DefaultBaseUri">The default base URI to be used for relative callback URLs, if any.</param>
/// <param name="Vars">A read-only dictionary containing variables relevant to the callback context.</param>
/// <param name="CallbackPayload">The payload object associated with the callback.</param>
public sealed record CallbackRuntimeContext(
    string CorrelationId,
    string IdempotencyKeySeed,
    Uri? DefaultBaseUri,
    IReadOnlyDictionary<string, object?> Vars,
    object? CallbackPayload
);
