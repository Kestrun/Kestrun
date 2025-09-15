using System.Reflection;
using Kestrun.Models;
using Kestrun.SharedState;

namespace Kestrun.Utilities;

/// <summary>
/// Provides utility methods for mapping and flattening variables from various sources.
/// </summary>
public static class VariablesMap
{
    /// <summary>
    /// Populates the provided dictionary with variables from the request context and shared state store.
    /// </summary>
    /// <param name="ctx">The Kestrun context containing request information.</param>
    /// <param name="vars">The dictionary to populate with variables.</param>
    /// <returns>True if variables were successfully mapped; otherwise, false.</returns>
    public static bool GetVariablesMap(KestrunContext ctx, ref Dictionary<string, object?> vars)
    {
        // ① Initialize the dictionary
        vars ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        return GetCommonProperties(ctx, ref vars) // ② Add common request properties
        && GetSharedStateStore(ref vars); // ③ Add shared state variables
    }

    /// <summary>
    /// Populates the provided dictionary with variables from the shared state store.
    /// </summary>
    /// <param name="vars">The dictionary to populate with shared state variables.</param>
    /// <returns>True if variables were successfully mapped; otherwise, false.</returns>
    public static bool GetSharedStateStore(ref Dictionary<string, object?> vars)
    {
        vars ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in SharedStateStore.Snapshot())
        {
            vars[kv.Key] = kv.Value; // 1) top-level JSON
        }

        return true;
    }

    /// <summary>
    /// Populates the provided dictionary with common request and server properties from the Kestrun context.
    /// </summary>
    /// <param name="ctx">The Kestrun context containing request information.</param>
    /// <param name="vars">The dictionary to populate with common properties.</param>
    /// <returns>True if properties were successfully mapped; otherwise, false.</returns>
    public static bool GetCommonProperties(KestrunContext ctx, ref Dictionary<string, object?> vars)
    {
        // ① Initialize the dictionary
        vars["Context"] = ctx;
        vars["Request"] = ctx.Request;
        vars["QueryString"] = ctx.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        vars["Form"] = ctx.Request.Form;
        vars["Cookies"] = ctx.Request.Cookies;
        vars["Headers"] = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        vars["UserAgent"] = ctx.Request.Headers["User-Agent"].ToString();
        vars["ServerVersion"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        vars["ServerOS"] = Environment.OSVersion.ToString();
        vars["ServerArch"] = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        vars["ServerIP"] = ctx.HttpContext.Connection.LocalIpAddress?.ToString() ?? "unknown";
        vars["ServerPort"] = ctx.HttpContext.Connection.LocalPort;
        vars["ServerName"] = Environment.MachineName;
        vars["Timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        return true;
    }
}

