using Kestrun.Health;
using Xunit;
using System.Runtime.InteropServices;
using System.Globalization;

namespace KestrunTests.Health;

public class ProcessProbeTests
{
    private static (string file, string args) CommandForExitCode(int code)
    {
        // Security / safety: ensure exit code is within normal POSIX/Win range (0-255) and not user-injected text.
        if (code is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(code), "Exit code must be between 0 and 255.");
        }
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
        // Build the argument string using invariant formatting to avoid localization surprises.
        var codeText = code.ToString(CultureInfo.InvariantCulture);
        // Quote the shell fragment to prevent interpretation if future changes introduce special chars.
        return ("/bin/sh", $"-c 'exit {codeText}'");
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

    [Fact]
    public async Task ProcessProbe_InternalTimeout_DegradedWithStdoutPreserved()
    {
        // We craft a command that emits some output and then sleeps longer than the timeout.
        string file;
        string args;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // cmd: echo then timeout for 5 seconds; we set probe timeout to 500ms
            file = "cmd.exe";
            args = "/c echo starting && ping -n 6 127.0.0.1 >NUL"; // approx 5s
        }
        else
        {
            // On Unix we avoid relying on shell builtins with buffering that might delay stdout capture.
            // Use 'sh -c "echo starting; sleep 5"'. Add 'stdbuf -o0' if available to flush immediately.
            // We keep it simple: many distros have /bin/sh (dash or bash) which flushes after newline for 'echo'.
            file = "/bin/sh";
            args = "-c 'echo starting; sleep 5'"; // 5s > 500ms timeout
        }

        var probe = new ProcessProbe("proctimeout", ["live"], file, args, TimeSpan.FromMilliseconds(500));
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Degraded, result.Status);
        if (result.Data is { } data && data.TryGetValue("stdout", out var captured) && captured is string s)
        {
            Assert.Contains("starting", s);
        }
        else
        {
            // Fallback: we still expect description to mention timeout, but stdout capture should ideally exist.
            Assert.Fail("Expected stdout data to be captured on timeout");
        }
    }
}
