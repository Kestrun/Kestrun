namespace Kestrun.Utilities;

/// <summary>
/// Helpers for determining the current environment name.
/// </summary>
public static class EnvironmentHelper
{
    private static Func<string?>? _overrideProvider;
    private static IHostEnvironment? _cachedEnv;

    /// <summary>
    /// Set a callback to provide an explicit environment name override.
    /// </summary>
    /// <param name="provider">The override provider function.</param>
    public static void SetOverride(Func<string?> provider)
        => _overrideProvider = provider;

    /// <summary>
    /// Set the host environment (typically from DI).
    /// </summary>
    /// <param name="env">The host environment.</param>
    public static void SetHostEnvironment(IHostEnvironment env)
        => _cachedEnv = env;

    /// <summary>
    /// Determine the current environment name.
    /// </summary>
    /// <returns> The current environment name.</returns>
    private static string ResolveEnvironment()
    {
        // 1️⃣ Explicit override
        var fromOverride = _overrideProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(fromOverride))
        {
            return fromOverride!;
        }

        // 2️⃣ Cached host environment (from SetHostEnvironment)
        if (!string.IsNullOrWhiteSpace(_cachedEnv?.EnvironmentName))
        {
            return _cachedEnv!.EnvironmentName;
        }

        // 3️⃣ Standard environment variables (like Kestrel)
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
    }

    /// <summary>
    /// The current environment name.
    /// </summary>
    public static string Name => ResolveEnvironment();

    /// <summary>
    /// Is the current environment "Development"?
    /// </summary>
    /// <returns>True if the current environment is "Development"; otherwise, false.</returns>
    public static bool IsDevelopment()
        => string.Equals(Name, Environments.Development, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Is the current environment "Staging"?
    /// </summary>
    /// <returns>True if the current environment is "Staging"; otherwise, false.</returns>
    public static bool IsStaging()
        => string.Equals(Name, Environments.Staging, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Is the current environment "Production"?
    /// </summary>
    /// <returns>True if the current environment is "Production"; otherwise, false.</returns>
    public static bool IsProduction()
        => string.Equals(Name, Environments.Production, StringComparison.OrdinalIgnoreCase);
}
