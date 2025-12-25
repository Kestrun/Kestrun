namespace Kestrun.Launcher.Utilities;

internal static class PathResolver
{
    private static readonly string[] DefaultStartupCandidates = new[] { "startup.ps1", "kestrun.ps1", "server.ps1" };

    public static string ResolveRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Root folder is required");
        }

        var fullPath = Path.GetFullPath(root);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Root folder does not exist: {fullPath}");
        }

        return fullPath;
    }

    public static string ResolveStartupScript(string root, string? startupOverride)
    {
        if (!string.IsNullOrWhiteSpace(startupOverride))
        {
            var candidate = Path.IsPathRooted(startupOverride)
                ? startupOverride
                : Path.Combine(root, startupOverride);

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            throw new FileNotFoundException($"Startup script not found at override path: {candidate}");
        }

        foreach (var candidate in DefaultStartupCandidates)
        {
            var path = Path.Combine(root, candidate);
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        throw new FileNotFoundException($"No startup script found in root '{root}'. Checked: {string.Join(", ", DefaultStartupCandidates)}");
    }
}
