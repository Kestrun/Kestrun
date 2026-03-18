#if NET10_0_OR_GREATER
using System.Reflection;
using Xunit;
#endif

namespace Kestrun.Runner.Tests.Runtime;

#if NET10_0_OR_GREATER
public class RunnerRuntimeProcessCaptureTests
{
    private static readonly MethodInfo RunProcessCaptureMethod = typeof(RunnerRuntime)
        .GetMethod("RunProcessCapture", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly Type ProcessResultType = typeof(RunnerRuntime)
        .GetNestedType("ProcessResult", BindingFlags.NonPublic)!;

    private static readonly PropertyInfo ExitCodeProperty = ProcessResultType.GetProperty("ExitCode")!;
    private static readonly PropertyInfo OutputProperty = ProcessResultType.GetProperty("Output")!;
    private static readonly PropertyInfo ErrorProperty = ProcessResultType.GetProperty("Error")!;

    [Fact]
    public async Task RunProcessCapture_WithLargeStdErrAndStdOut_CompletesAndCapturesBothStreams()
    {
        var (fileName, arguments) = BuildLargeStreamCommand();

        // If stream consumption regresses to a deadlock-prone pattern, this invocation will not complete.
        var invocationTask = Task.Run(() => InvokeRunProcessCapture(fileName, arguments));
        var completedTask = await Task.WhenAny(invocationTask, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.True(ReferenceEquals(completedTask, invocationTask), "RunProcessCapture timed out, indicating potential stdout/stderr deadlock.");

        var (ExitCode, Output, Error) = await invocationTask;
        Assert.Equal(0, ExitCode);
        Assert.Contains("OUTLINE_1", Output, StringComparison.Ordinal);
        Assert.Contains("OUTLINE_12000", Output, StringComparison.Ordinal);
        Assert.Contains("ERRLINE_1", Error, StringComparison.Ordinal);
        Assert.Contains("ERRLINE_12000", Error, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output, string Error) InvokeRunProcessCapture(string fileName, string[] arguments)
    {
        var result = RunProcessCaptureMethod.Invoke(null, [fileName, arguments]);
        Assert.NotNull(result);

        return (
            ExitCode: (int)ExitCodeProperty.GetValue(result!)!,
            Output: (string)OutputProperty.GetValue(result!)!,
            Error: (string)ErrorProperty.GetValue(result!)!);
    }

    private static (string FileName, string[] Arguments) BuildLargeStreamCommand()
    {
        const int lineCount = 12000;

        if (OperatingSystem.IsWindows())
        {
            var command = $"(for /L %i in (1,1,{lineCount}) do @echo ERRLINE_%i 1>&2) & (for /L %i in (1,1,{lineCount}) do @echo OUTLINE_%i)";
            return ("cmd.exe", ["/c", command]);
        }

        var shellCommand = $"i=1; while [ $i -le {lineCount} ]; do printf 'ERRLINE_%s\\n' \"$i\" >&2; i=$((i+1)); done; i=1; while [ $i -le {lineCount} ]; do printf 'OUTLINE_%s\\n' \"$i\"; i=$((i+1)); done";
        return ("/bin/sh", ["-c", shellCommand]);
    }
}
#endif
