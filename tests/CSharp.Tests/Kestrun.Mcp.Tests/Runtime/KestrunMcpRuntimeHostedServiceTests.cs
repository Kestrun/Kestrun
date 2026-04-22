using Kestrun.Mcp.ServerHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kestrun.Mcp.Tests.Runtime;

public sealed class KestrunMcpRuntimeHostedServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public KestrunMcpRuntimeHostedServiceTests()
    {
        KestrunHostManager.DestroyAll();
    }

    public void Dispose()
    {
        KestrunHostManager.DestroyAll();

        foreach (var path in _tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task StartAsync_CompletesRuntimeWithError_WhenScriptDoesNotRegisterExpectedHost()
    {
        var runtime = new KestrunMcpRuntime();
        var options = new KestrunMcpCommandLine(
            ScriptPath: CreateScript("# Intentionally does not create a Kestrun host."),
            ModuleManifestPath: ResolveModuleManifestPath(),
            HostName: $"missing-host-{Guid.NewGuid():N}",
            DiscoverPowerShellHome: true,
            AllowInvokeRoute: false,
            AllowedInvokePaths: []);

        using var stoppingCts = new CancellationTokenSource();
        var appLifetime = new Mock<IHostApplicationLifetime>(MockBehavior.Strict);
        _ = appLifetime.SetupGet(static lifetime => lifetime.ApplicationStopping).Returns(stoppingCts.Token);
        _ = appLifetime.Setup(static lifetime => lifetime.StopApplication());

        var service = new KestrunScriptSessionHostedService(
            options,
            runtime,
            NullLogger<KestrunScriptSessionHostedService>.Instance,
            appLifetime.Object);

        await service.StartAsync(CancellationToken.None);

        using var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.WaitForHostAsync(waitCts.Token));

        Assert.Contains("completed without registering", exception.Message, StringComparison.Ordinal);
        Assert.Contains(options.HostName!, exception.Message, StringComparison.Ordinal);
        appLifetime.Verify(static lifetime => lifetime.StopApplication(), Times.Once);

        await service.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Creates a temporary PowerShell script for hosted-service execution.
    /// </summary>
    /// <param name="contents">Script contents.</param>
    /// <returns>The created script path.</returns>
    private string CreateScript(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kestrun-mcp-runtime-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(path, contents);
        _tempFiles.Add(path);
        return path;
    }

    /// <summary>
    /// Resolves the local Kestrun module manifest path from the repository root.
    /// </summary>
    /// <returns>The absolute module manifest path.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the repository-local manifest cannot be found.</exception>
    private static string ResolveModuleManifestPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "PowerShell", "Kestrun", "Kestrun.psd1");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Unable to locate src/PowerShell/Kestrun/Kestrun.psd1 from the test output directory.");
    }
}
