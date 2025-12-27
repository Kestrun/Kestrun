namespace Kestrun.Launcher.Utilities;

internal static class PlatformGuard
{
    public static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This command is only supported on Windows.");
        }
    }
}
