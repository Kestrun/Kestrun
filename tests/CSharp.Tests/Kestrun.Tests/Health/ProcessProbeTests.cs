using Kestrun.Health;
using Xunit;
using System.Runtime.InteropServices;

namespace KestrunTests.Health;

public class ProcessProbeTests
{
    private static (string file, string args) CommandForExitCode(int code)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("cmd.exe", $"/c exit {code}");
        }
        // NOTE: Using /usr/bin/env with true/false avoids shell quoting issues when invoking /bin/sh -c 'exit X'
        // directly via ProcessStartInfo (quotes are not stripped, leading to command-not-found and exit 127 on Unix).
        // true exits 0; false exits 1. For other codes (not used currently), still fall back to /bin/sh form.
        if (code == 0)
        {
            return ("/usr/bin/env", "true");
        }
        if (code == 1)
        {
            return ("/usr/bin/env", "false");
        }
        return ("/bin/sh", $"-c exit {code}");
    }

    [Fact]
    public async Task ProcessProbe_ExitCode0_Healthy()
    {
        var (file, args) = CommandForExitCode(0);
        var probe = new ProcessProbe("proc0", ["live"], file, args);
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ProcessProbe_ExitCode1_Degraded()
    {
        var (file, args) = CommandForExitCode(1);
        var probe = new ProcessProbe("proc1", ["live"], file, args);
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Degraded, result.Status);
    }

    // NOTE: JSON contract parsing path already covered indirectly by HttpProbe tests; process variant omitted due to
    // platform-specific echo / quoting inconsistencies that made test flaky across shells.
}
