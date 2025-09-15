using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
namespace Kestrun.Hosting.Options;

/// <summary>
/// Provides extension methods for copying configuration between <see cref="KestrelServerOptions"/> instances.
/// </summary>
public static class KestrelOptionsExtensions
{
    /// <summary>
    /// Shallow-copies every writable property from <paramref name="src"/> to <paramref name="dest"/>,
    /// then deep-copies the nested <see cref="KestrelServerLimits"/>.
    /// A small “skip” list prevents us from overwriting framework internals.
    /// </summary>
    public static void CopyFromTemplate(this KestrelServerOptions dest,
                                        KestrelServerOptions src)
    {
        if (dest is null || src is null)
        {
            throw new ArgumentNullException();
        }

        var skip = new HashSet<string>
        {
            nameof(KestrelServerOptions.ApplicationServices), // owned by the WebHost
           //nameof(KestrelServerOptions.ListenOptions)        // you add those yourself
        };

        // ── 1. copy all simple writable props ───────────────────────────────
        foreach (var p in typeof(KestrelServerOptions)
                                   .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite)
            {
                continue;              // read-only
            }

            if (p.GetIndexParameters().Length > 0)
            {
                continue;              // indexer
            }

            if (skip.Contains(p.Name))
            {
                continue;              // infrastructure
            }

            p.SetValue(dest, p.GetValue(src));
        }

        // ── 2. deep-copy the Limits object (property itself is read-only) ──
        CopyLimits(dest.Limits, src.Limits);
    }

    private static void CopyLimits(KestrelServerLimits dest, KestrelServerLimits src)
    {
        foreach (var p in typeof(KestrelServerLimits)
                                   .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite)
            {
                continue;
            }

            if (p.GetIndexParameters().Length > 0)
            {
                continue;
            }

            p.SetValue(dest, p.GetValue(src));
        }
    }
}
