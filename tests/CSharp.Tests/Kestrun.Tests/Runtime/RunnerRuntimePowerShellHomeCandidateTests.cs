#if NET10_0_OR_GREATER
using System.Reflection;
using Kestrun.Runner;
using Xunit;
#endif

namespace KestrunTests.Runtime;

#if NET10_0_OR_GREATER
public class RunnerRuntimePowerShellHomeCandidateTests
{
    private static readonly MethodInfo NormalizeCandidateMethod = typeof(RunnerRuntime)
        .GetMethod("NormalizePowerShellHomeCandidate", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void NormalizePowerShellHomeCandidate_WithExecutableUnderNonPowerShellDirectory_ReturnsEmpty()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "kestrun-runner-tests", Guid.NewGuid().ToString("N"));
        var binDirectory = Path.Combine(tempRoot, "usr", "bin");
        var executablePath = Path.Combine(binDirectory, "pwsh");

        Directory.CreateDirectory(binDirectory);
        File.WriteAllText(executablePath, "#!/bin/sh\nexit 0\n");

        try
        {
            var normalized = InvokeNormalizePowerShellHomeCandidate(executablePath);
            Assert.Equal(string.Empty, normalized);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void NormalizePowerShellHomeCandidate_WithExecutableInValidPowerShellHome_ReturnsInstallDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "kestrun-runner-tests", Guid.NewGuid().ToString("N"));
        var psHome = Path.Combine(tempRoot, "opt", "microsoft", "powershell", "7");
        var moduleDirectory = Path.Combine(psHome, "Modules", "Microsoft.PowerShell.Management");
        var moduleManifestPath = Path.Combine(moduleDirectory, "Microsoft.PowerShell.Management.psd1");
        var executablePath = Path.Combine(psHome, "pwsh");

        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(moduleManifestPath, "@{ CompatiblePSEditions = @('Core') }\n");
        File.WriteAllText(executablePath, "#!/bin/sh\nexit 0\n");

        try
        {
            var normalized = InvokeNormalizePowerShellHomeCandidate(executablePath);
            Assert.Equal(Path.GetFullPath(psHome), normalized);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string InvokeNormalizePowerShellHomeCandidate(string candidatePath)
    {
        return (string)NormalizeCandidateMethod.Invoke(null, [candidatePath])!;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temporary test files.
        }
    }
}
#endif
