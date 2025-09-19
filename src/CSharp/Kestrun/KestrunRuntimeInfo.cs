using System.Reflection;
using System.Runtime.Versioning;

namespace Kestrun;

/// <summary>
/// Utility class to expose information about the runtime environment
/// that Kestrun was built for, and to gate features by TFM and runtime.
/// </summary>
public static class KestrunRuntimeInfo
{
    /// <summary>
    /// Returns the target framework version this assembly was built against
    /// as a System.Version (e.g., 8.0, 9.0).
    /// </summary>
    public static Version GetBuiltTargetFrameworkVersion()
    {
        var asm = typeof(KestrunRuntimeInfo).Assembly;
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

    // --- Feature gating ---

    /// <summary>
    /// Built-in Kestrun feature keys. Add more as you gate new APIs by TFM.
    /// </summary>
    public enum KnownFeature
    {
        /// <summary>
        /// Kestrel HTTP/3 listener support (requires QUIC at runtime)
        /// </summary>
        Http3 = 0,
    }

    // Minimal TFM required for each feature.
    // Extend this table as you add features.
    private static readonly Dictionary<string, Version> FeatureMinByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(KnownFeature.Http3)] = new Version(8, 0),
        };

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
    public static bool TryGetMinVersion(string featureName, out Version minVersion)
        => FeatureMinByName.TryGetValue(featureName, out minVersion!);

    private static bool IsAtLeast(Version min)
    {
        var built = GetBuiltTargetFrameworkVersion();
        return built >= min;
    }

    #region Runtime checks
    // ---------- Runtime-sensitive checks ----------
    /// <summary>
    /// Checks runtime support for HTTP/3 (QUIC) in addition to compile-time gating.
    /// </summary>
    private static bool CheckHttp3Runtime()
    {
        // Check presence of System.Net.Quic and whether it says it's supported.
        var quicListenerType = Type.GetType("System.Net.Quic.QuicListener, System.Net.Quic");
        if (quicListenerType == null)
        {
            return false;
        }

        var isSupportedProp = quicListenerType.GetProperty("IsSupported", BindingFlags.Public | BindingFlags.Static);
        if (isSupportedProp != null && isSupportedProp.PropertyType == typeof(bool))
        {
            var value = isSupportedProp.GetValue(null);
            if (value is bool supported)
            {
                if (!supported)
                {
                    return false;
                }
            }
        }
        // If no IsSupported property, presence of the type usually implies support.

        // Also confirm Kestrel has Http3 enum value available
        var httpProtocolsType = Type.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols, Microsoft.AspNetCore.Server.Kestrel.Core");
        if (httpProtocolsType == null)
        {
            return false;
        }

        var http3Field = httpProtocolsType.GetField("Http3", BindingFlags.Public | BindingFlags.Static);
        return http3Field != null;
    }
    #endregion

    /// <summary>
    /// Convenience, if you ever want a string like ".NETCoreApp,Version=v9.0"
    /// </summary>
    public static string GetBuiltTargetFrameworkMoniker()
    {
        var tfm = typeof(KestrunRuntimeInfo).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        return tfm ?? "Unknown";
    }
}
