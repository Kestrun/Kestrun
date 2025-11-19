namespace Kestrun.Runtime;

/// <summary>
/// Helpers for determining the current environment name.
/// </summary>
public static class EnvironmentHelper
{
    private static Func<string?>? _overrideProvider;
    private static IHostEnvironment? _cachedEnv;

    /// <summary>
    /// Set the host environment (usually from DI).
    /// </summary>
    /// <param name="env">The host environment.</param>
    public static void SetHostEnvironment(IHostEnvironment env) => _cachedEnv = env;

    /// <summary>
    /// Set an explicit override for the environment name.
    /// </summary>
    /// <param name="name">The environment name to override with.</param>
    public static void SetOverrideName(string? name)
        => _overrideProvider = string.IsNullOrWhiteSpace(name) ? null : () => name;

    /// <summary>
    /// Set an explicit override provider for the environment name.
    /// </summary>
    /// <param name="provider">The provider function to retrieve the environment name.</param>
    public static void SetOverride(Func<string?> provider) => _overrideProvider = provider;

    /// <summary>
    /// Clear any explicit override for the environment name.
    /// </summary>
    public static void ClearOverride() => _overrideProvider = null;

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
            return fromOverride;
        }

        // 2️⃣ Cached host environment (from SetHostEnvironment)
        if (!string.IsNullOrWhiteSpace(_cachedEnv?.EnvironmentName))
        {
            return _cachedEnv.EnvironmentName;
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
