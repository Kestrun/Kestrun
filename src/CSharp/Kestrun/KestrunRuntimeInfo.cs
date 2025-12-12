using System.Reflection;
using System.Runtime.Versioning;
using System.Diagnostics.CodeAnalysis;

namespace Kestrun;

/// <summary>
/// Utility class to expose information about the runtime environment
/// that Kestrun was built for, and to gate features by TFM and runtime.
/// </summary>
public static class KestrunRuntimeInfo
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
    /// Built-in Kestrun feature keys. Add more as you gate new APIs by TFM.
    /// </summary>
    public enum KnownFeature
    {
        /// <summary>
        /// Kestrel HTTP/3 listener support (requires QUIC at runtime)
        /// </summary>
        Http3 = 0,
        /// <summary>
        /// Suppresses reading the antiforgery token from the form body
        /// </summary>
        SuppressReadingTokenFromFormBody = 1
    }

    // Minimal TFM required for each feature.
    // Extend this table as you add features.
    private static readonly Dictionary<string, Version> FeatureMinByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(KnownFeature.Http3)] = new Version(8, 0),
            // New in .NET 9+: Antiforgery option to suppress reading token from request form body.
            [nameof(KnownFeature.SuppressReadingTokenFromFormBody)] = new Version(9, 0),
        };

    /// <summary>
    /// Returns the set of known feature identifiers (enum names) that have a
    /// compile-time (TFM) gate registered. This does not guarantee that
    /// <see cref="Supports(string)"/> will return true, only that the feature
    /// is recognized and has a minimum version entry.
    /// </summary>
    public static IEnumerable<string> GetKnownFeatures() => FeatureMinByName.Keys;

    /// <summary>
    /// True if the loaded Kestrun assembly supports the feature,
    /// considering both build-time TFM and runtime requirements.
    /// </summary>
    public static bool Supports(KnownFeature feature)
    {
        var name = feature.ToString();
        if (!TryGetMinVersion(name, out var min) || !IsAtLeast(min))
        {
            return false; // compile-time gate failed
        }

        // runtime-sensitive checks
        return feature switch
        {
            KnownFeature.Http3 => CheckHttp3Runtime(),
            _ => true,
        };
    }

    /// <summary>
    /// True if the loaded Kestrun assembly supports the feature,
    /// considering both build-time TFM and runtime requirements.
    /// Unknown names return false.
    /// </summary>
    public static bool Supports(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return false;
        }

        if (!TryGetMinVersion(featureName, out var min) || !IsAtLeast(min))
        {
            return false; // compile-time/TFM gate failed or unknown
        }

        // Central runtime-sensitive feature dispatch (mirrors enum switch in Supports(KnownFeature))
        // Extend this map when adding new runtime-validated features.
        // For features with no runtime conditions, omission implies success.
        var runtimeChecks = RuntimeFeatureChecks;
        if (runtimeChecks.TryGetValue(featureName, out var checker))
        {
            return checker();
        }
        return true; // Known (by TFM table) & no runtime check required
    }

    // Provides a single place to register runtime-sensitive checks by feature name.
    // Note: Uses StringComparer.OrdinalIgnoreCase to keep behavior consistent with FeatureMinByName.
    private static readonly Dictionary<string, Func<bool>> RuntimeFeatureChecks =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(KnownFeature.Http3)] = CheckHttp3Runtime,
        };

    /// <summary>
    /// Returns the minimum TFM required for a feature, if known.
    /// </summary>
    /// <param name="featureName">Feature identifier (case-insensitive).</param>
    /// <param name="minVersion">
    /// When this method returns true, contains the minimum target framework version required for the feature.
    /// When it returns false, the value is set to <c>0.0</c> as a harmless placeholder.
    /// </param>
    /// <returns>True if the feature is known; otherwise false.</returns>
    public static bool TryGetMinVersion(string featureName, [NotNullWhen(true)] out Version minVersion)
    {
        if (FeatureMinByName.TryGetValue(featureName, out var value))
        {
            minVersion = value; // non-null by construction
            return true;
        }

        // Provide a non-null sentinel to satisfy non-nullable contract while indicating absence.
        minVersion = new Version(0, 0);
        return false;
    }

    private static bool IsAtLeast(Version min)
    {
        var built = KestrunAnnotationsRuntimeInfo.GetBuiltTargetFrameworkVersion();
        return built >= min;
    }

    #region Runtime checks
    // ---------- Runtime-sensitive checks ----------
    /// <summary>
    /// Checks runtime support for HTTP/3 (QUIC) in addition to compile-time gating.
    /// </summary>
    private static bool CheckHttp3Runtime()
    {
        // Use cached reflection metadata to avoid repeated lookups on hot paths.
        if (_quicListenerType == null)
        {
            return false;
        }

        if (_quicIsSupportedProperty != null)
        {
            var value = _quicIsSupportedProperty.GetValue(null);
            if (value is bool supported && !supported)
            {
                return false;
            }
        }

        // Kestrel HTTP/3 enum field presence check
        return _httpProtocolsType != null && _http3Field != null;
    }
    #endregion

    // Cached reflection metadata for runtime checks (initialized once).
    private static readonly Type? _quicListenerType = Type.GetType("System.Net.Quic.QuicListener, System.Net.Quic");
    private static readonly PropertyInfo? _quicIsSupportedProperty = _quicListenerType?.GetProperty("IsSupported", BindingFlags.Public | BindingFlags.Static);
    private static readonly Type? _httpProtocolsType = Type.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols, Microsoft.AspNetCore.Server.Kestrel.Core");
    private static readonly FieldInfo? _http3Field = _httpProtocolsType?.GetField("Http3", BindingFlags.Public | BindingFlags.Static);

    /// <summary>
    /// Returns the full target framework name this assembly was built against,
    /// e.g., ".NETCoreApp,Version=v9.0".
    /// </summary>
    public static string GetBuiltTargetFrameworkName()
    {
        var tfm = typeof(KestrunRuntimeInfo).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        return tfm ?? "Unknown";
    }
}
