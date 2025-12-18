using System.Runtime.CompilerServices;

namespace KestrunTests;

internal static class TestEnvironmentBootstrap
{
    [ModuleInitializer]
    internal static void Init()
    {
        // On Linux/WSL, the default config/file-provider plumbing uses FileSystemWatcher (inotify).
        // Large test suites that construct many hosts can exhaust the inotify instance limit.
        // Polling avoids inotify and stabilizes CI/local Linux runs.
        if (OperatingSystem.IsLinux())
        {
            Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        }
    }
}
