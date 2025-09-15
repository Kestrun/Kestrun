namespace Kestrun.Authentication;

/// <summary>
/// Specifies the format for API key authentication challenges.
/// </summary>
public enum ApiKeyChallengeFormat
{
    /// <summary>
    /// Emits: <c>ApiKey header="X-Api-Key"</c>
    /// </summary>
    ApiKeyHeader,

    /// <summary>
    /// Emits: <c>X-Api-Key</c>
    /// </summary>
    HeaderOnly
}
