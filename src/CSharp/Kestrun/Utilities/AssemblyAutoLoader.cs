//  File: AssemblyAutoLoader.cs
//  Namespace: Kestrun.Utilities   (choose any namespace that suits you)

using System.Reflection;
using System.Runtime.Loader;

namespace Kestrun.Utilities;

/// <summary>
///  Registers one or more folders that contain private assemblies and makes
///  sure every DLL in those folders is available to PowerShell / scripts.
///  Call <see cref="PreloadAll"/> **once** at startup (or from PowerShell)
///  and forget about “could not load assembly …” errors.
/// </summary>
public static class AssemblyAutoLoader
{
    private static readonly HashSet<string> _searchDirs =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool _hookInstalled;
    private static bool _verbose;

#if NET9_0_OR_GREATER
    private static readonly Lock _gate = new();
#else
    private static readonly object _gate = new();
#endif

    /// <summary>
    ///  Scans the supplied directories, loads every DLL that isn’t already
    ///  loaded, and installs an <c>AssemblyResolve</c> hook so that any
    ///  later requests are resolved automatically.
    /// </summary>
    /// <param name="verbose">
    ///  If <see langword="true"/>, outputs diagnostic information to the console.
    /// </param>
    /// <param name="directories">
    ///  One or more absolute paths (they may be repeated; duplicates ignored).
    /// </param>
    /// <remarks>
    ///  You can call this more than once — new folders are merged into the
    ///  internal set, previously scanned ones are skipped.
    /// </remarks>
    public static void PreloadAll(bool verbose = false, params string[] directories)
    {
        if (directories is null || directories.Length == 0)
        {
            throw new ArgumentException("At least one folder is required.", nameof(directories));
        }

        lock (_gate)
        {
            _verbose = verbose;
            // Remember new folders
            foreach (var dir in directories.Where(Directory.Exists))
            {
                if (_verbose)
                {
                    // Use Console.WriteLine for simplicity, or use your logging framework
                    Console.WriteLine($"Adding search directory: {dir}");
                }

                if (_searchDirs.Contains(dir))
                {
                    continue; // skip duplicates
                }

                _ = _searchDirs.Add(Path.GetFullPath(dir));
            }

            // Install the resolve hook once
            if (!_hookInstalled)
            {
                if (_verbose)
                {
                    // Use Console.WriteLine for simplicity, or use your logging framework
                    Console.WriteLine("Installing AssemblyResolve hook for Kestrun.Utilities");
                }
                // This will be called whenever the runtime fails to find an assembly
                AppDomain.CurrentDomain.AssemblyResolve += ResolveFromSearchDirs;
                _hookInstalled = true;
            }

            // Pre-load everything so types are immediately available
            foreach (var dir in _searchDirs)
            {
                foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"Pre-loading assembly: {dll}");
                    }

                    _ = SafeLoad(dll);
                }
            }
        }
    }

    // ---------------- helpers ----------------

    private static Assembly? ResolveFromSearchDirs(object? sender, ResolveEventArgs args)
    {
        if (args is null || string.IsNullOrEmpty(args.Name))
        {
            if (_verbose)
            {
                Console.WriteLine("ResolveFromSearchDirs called with null or empty name.");
            }

            return null; // let the runtime continue searching
        }
        var shortName = new AssemblyName(args.Name).Name + ".dll";
        if (_verbose)
        {
            Console.WriteLine($"Resolving assembly: {shortName}");
        }

        foreach (var dir in _searchDirs)
        {
            var candidate = Path.Combine(dir, shortName);
            if (File.Exists(candidate))
            {
                if (_verbose)
                {
                    Console.WriteLine($"Resolving assembly: {candidate}");
                }

                return SafeLoad(candidate);
            }
        }
        return null; // let the runtime continue searching
    }

    private static Assembly? SafeLoad(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        // Skip if already loaded
        if (AppDomain.CurrentDomain.GetAssemblies()
              .Any(a => string.Equals(a.GetName().Name, name,
                                      StringComparison.OrdinalIgnoreCase)))
        {
            if (_verbose)
            {
                Console.WriteLine($"Assembly '{name}' is already loaded, skipping: {path}");
            }
            // Return null to indicate no new assembly was loaded
            return null;
        }

        try
        {
            if (_verbose)
            {
                Console.WriteLine($"Loading assembly: {path}");
            }
            // Load the assembly from the specified path (Core-friendly)
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
        }
        catch
        {
            if (_verbose)
            {
                Console.WriteLine($"Failed to load assembly: {path}");
            }
            // Swallow – we don’t block startup because of one bad DLL
            return null;
        }
    }
    /// <summary>
    /// Detaches the <c>AssemblyResolve</c> hook and, optionally, clears the
    /// list of search-directories.  Call this at the end of a runspace or
    /// when the application no longer needs dynamic resolution.
    /// </summary>
    /// <param name="clearSearchDirs">
    /// <see langword="true"/> ⇒ also forget the registered folders.
    /// Leave it <see langword="false"/> if you want to keep the list so a
    /// later <c>PreloadAll()</c> call can reuse it without re-scanning.
    /// </param>
    public static void Clear(bool clearSearchDirs = false)
    {
        lock (_gate)
        {
            if (_hookInstalled)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= ResolveFromSearchDirs;
                _hookInstalled = false;

                if (_verbose)
                {
                    Console.WriteLine("AssemblyResolve hook removed.");
                }
            }

            if (clearSearchDirs && _searchDirs.Count > 0)
            {
                _searchDirs.Clear();
                if (_verbose)
                {
                    Console.WriteLine("Search-directory list cleared.");
                }
            }
        }
    }
}
