using System.Reflection;
using System.Runtime.Versioning;


/// <summary>
/// Utility class to expose information about the runtime environment
/// that Kestrun was built for, and to gate features by TFM and runtime.
/// </summary>
public static class KestrunAnnotationsRuntimeInfo
{
    /// <summary>
    /// Determines whether the current distribution is a release distribution.
    /// </summary>
    /// <returns> True if the current distribution is a release distribution; otherwise, false.</returns>
#if DEBUG
    public static bool IsReleaseDistribution => false;
#else
    public static bool IsReleaseDistribution => true;
#endif

    /// <summary>
    /// Determines whether the current build is a debug build.
    /// </summary>
    /// <returns> True if the current build is a debug build; otherwise, false.</returns>
    public static bool IsDebugBuild => !IsReleaseDistribution;

    /// <summary>
    /// Returns the target framework version this assembly was built against
    /// as a System.Version (e.g., 8.0, 9.0).
    /// </summary>
    public static Version GetBuiltTargetFrameworkVersion()
    {
        var asm = typeof(KestrunAnnotationsRuntimeInfo).Assembly;
        var tfm = asm.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (string.IsNullOrEmpty(tfm))
        {
            return new Version(0, 0);
        }

        var key = "Version=";
        var idx = tfm.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var ver = tfm[(idx + key.Length)..].TrimStart('v');
            if (Version.TryParse(ver, out var parsed))
            {
                return parsed;
            }
        }

        return new Version(0, 0);
    }
}
