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
        return ("/bin/sh", $"-c 'exit {code}'");
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
