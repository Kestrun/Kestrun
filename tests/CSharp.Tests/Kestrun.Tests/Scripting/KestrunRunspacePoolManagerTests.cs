using System.Management.Automation.Runspaces;
using Kestrun.Hosting;
using Kestrun.Scripting;
using Serilog;
using Xunit;

namespace KestrunTests.Scripting;

[Trait("Category", "Scripting")]
[Collection("RunspaceSerial")]
public class KestrunRunspacePoolManagerTests : IDisposable
{
    private readonly KestrunHost _host;

    public KestrunRunspacePoolManagerTests() => _host = new KestrunHost("Tests", Log.Logger);

    public void Dispose() => _host?.Dispose();

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        using var manager = new KestrunRunspacePoolManager(_host, 2, 5);

        // Assert
        Assert.Equal(2, manager.MinRunspaces);
        Assert.Equal(5, manager.MaxRunspaces);
        Assert.Equal(_host, manager.Host);
    }

    [Fact]
    public void Constructor_WithZeroMinRunspaces_Succeeds()
    {
        // Arrange & Act
        using var manager = new KestrunRunspacePoolManager(_host, 0, 3);

        // Assert
        Assert.Equal(0, manager.MinRunspaces);
        Assert.Equal(3, manager.MaxRunspaces);
    }

    [Fact]
    public void Constructor_WithNegativeMinRunspaces_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KestrunRunspacePoolManager(_host, -1, 5));
    }

    [Fact]
    public void Constructor_WithNegativeMaxRunspaces_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KestrunRunspacePoolManager(_host, 1, -1));
    }

    [Fact]
    public void Constructor_WithMaxLessThanMin_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KestrunRunspacePoolManager(_host, 5, 2));
    }

    [Fact]
    public void Constructor_WithZeroMaxRunspaces_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KestrunRunspacePoolManager(_host, 0, 0));
    }

    [Fact]
    public void Constructor_WithNullHost_ThrowsNullReferenceException()
    {
        // Act & Assert
        _ = Assert.Throws<NullReferenceException>(() =>
            new KestrunRunspacePoolManager(null!, 1, 5));
    }

    [Fact]
    public void Constructor_WithCustomThreadOptions_SetsProperty()
    {
        // Arrange & Act
        using var manager = new KestrunRunspacePoolManager(
            _host, 1, 3, threadOptions: PSThreadOptions.UseNewThread);

        // Assert
        Assert.Equal(PSThreadOptions.UseNewThread, manager.ThreadOptions);
    }

    [Fact]
    public void Constructor_WithOpenApiClassesPath_SetsProperty()
    {
        // Arrange
        var path = "test/path/classes.ps1";

        // Act
        using var manager = new KestrunRunspacePoolManager(_host, 1, 3)
        {
            OpenApiClassesPath = path
        };

        // Assert
        Assert.Equal(path, manager.OpenApiClassesPath);
    }

    [Fact]
    public void ThreadOptions_CanBeSetAfterConstruction()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 1, 3);

        // Act
        manager.ThreadOptions = PSThreadOptions.UseCurrentThread;

        // Assert
        Assert.Equal(PSThreadOptions.UseCurrentThread, manager.ThreadOptions);
    }

    [Fact]
    public void Acquire_ReturnsOpenedRunspace()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 1, 3);

        // Act
        var runspace = manager.Acquire();

        // Assert
        Assert.NotNull(runspace);
        Assert.Equal(RunspaceState.Opened, runspace.RunspaceStateInfo.State);

        // Cleanup
        manager.Release(runspace);
    }

    [Fact]
    public void Acquire_WithMinRunspaces_ReusesFromPool()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 2, 5);

        // Act - Acquire and release to populate pool
        var rs1 = manager.Acquire();
        manager.Release(rs1);
        var rs2 = manager.Acquire();

        // Assert - Should get same runspace back
        Assert.Same(rs1, rs2);

        // Cleanup
        manager.Release(rs2);
    }

    [Fact]
    public void Acquire_BeyondMaxRunspaces_ThrowsInvalidOperationException()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 0, 2);
        var rs1 = manager.Acquire();
        var rs2 = manager.Acquire();

        // Act & Assert
        _ = Assert.Throws<InvalidOperationException>(manager.Acquire);

        // Cleanup
        manager.Release(rs1);
        manager.Release(rs2);
    }

    [Fact]
    public void Acquire_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var manager = new KestrunRunspacePoolManager(_host, 1, 3);
        manager.Dispose();

        // Act & Assert
        _ = Assert.Throws<ObjectDisposedException>(manager.Acquire);
    }

    [Fact]
    public async Task AcquireAsync_ReturnsOpenedRunspace()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 1, 3);

        // Act
        var runspace = await manager.AcquireAsync();

        // Assert
        Assert.NotNull(runspace);
        Assert.Equal(RunspaceState.Opened, runspace.RunspaceStateInfo.State);

        // Cleanup
        manager.Release(runspace);
    }

    [Fact]
    public async Task AcquireAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 0, 1);
        var rs = manager.Acquire(); // Fill pool
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        _ = await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await manager.AcquireAsync(cts.Token));

        // Cleanup
        manager.Release(rs);
    }

    [Fact]
    public async Task AcquireAsync_WaitsForAvailableRunspace()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 0, 1);
        var rs1 = manager.Acquire();

        // Act - Start async acquire that should wait
        var acquireTask = Task.Run(async () => await manager.AcquireAsync());
        await Task.Delay(100); // Let it start waiting

        // Release the runspace to unblock
        manager.Release(rs1);
        var rs2 = await acquireTask;

        // Assert
        Assert.NotNull(rs2);

        // Cleanup
        manager.Release(rs2);
    }

    [Fact]
    public void Release_ReturnsRunspaceToPool()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 1, 3);
        var rs1 = manager.Acquire();

        // Act
        manager.Release(rs1);
        var rs2 = manager.Acquire();

        // Assert - Should get same runspace back
        Assert.Same(rs1, rs2);

        // Cleanup
        manager.Release(rs2);
    }

    [Fact]
    public void Release_AfterDispose_DisposesRunspace()
    {
        // Arrange
        var manager = new KestrunRunspacePoolManager(_host, 1, 3);
        var rs = manager.Acquire();
        manager.Dispose();

        // Act - Should not throw
        manager.Release(rs);

        // Assert - Runspace should be disposed
        Assert.Equal(RunspaceState.Closed, rs.RunspaceStateInfo.State);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var manager = new KestrunRunspacePoolManager(_host, 1, 3);

        // Act & Assert - Should not throw
        manager.Dispose();
        manager.Dispose();
        manager.Dispose();
    }

    [Fact]
    public void Dispose_ClosesAllRunspaces()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 2, 3);
        var rs1 = manager.Acquire();
        var rs2 = manager.Acquire();

        // Act
        manager.Dispose();

        // Assert
        Assert.Equal(RunspaceState.Closed, rs1.RunspaceStateInfo.State);
        Assert.Equal(RunspaceState.Closed, rs2.RunspaceStateInfo.State);
    }

    [Fact]
    public async Task ConcurrentAcquire_WithinLimit_AllSucceed()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 0, 10);
        var tasks = new List<Task<Runspace>>();

        // Act
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(manager.Acquire));
        }

        var runspaces = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, runspaces.Length);
        Assert.All(runspaces, rs => Assert.Equal(RunspaceState.Opened, rs.RunspaceStateInfo.State));

        // Cleanup
        foreach (var rs in runspaces)
        {
            manager.Release(rs);
        }
    }

    [Fact]
    public async Task ConcurrentAcquireRelease_MaintainsPoolIntegrity()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 0, 5);
        var semaphore = new SemaphoreSlim(5); // Limit to max runspaces
        var tasks = new List<Task>();

        // Act - Multiple concurrent acquire/release cycles
        for (var i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(); // Ensure we don't exceed pool limit
                try
                {
                    var rs = manager.Acquire();
                    await Task.Delay(5); // Simulate work
                    manager.Release(rs);
                }
                finally
                {
                    _ = semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Pool should still be functional
        var testRs = manager.Acquire();
        Assert.NotNull(testRs);
        Assert.Equal(RunspaceState.Opened, testRs.RunspaceStateInfo.State);

        // Cleanup
        manager.Release(testRs);
    }

    [Fact]
    public void Acquire_CreatesRunspaceWithConfiguredThreadOptions()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(
            _host, 0, 2, threadOptions: PSThreadOptions.UseCurrentThread);

        // Act
        var rs = manager.Acquire();

        // Assert
        Assert.Equal(PSThreadOptions.UseCurrentThread, rs.ThreadOptions);

        // Cleanup
        manager.Release(rs);
    }

    [Fact]
    public void Acquire_CreatesRunspaceWithMTAApartmentState()
    {
        // Arrange
        using var manager = new KestrunRunspacePoolManager(_host, 0, 2);

        // Act
        var rs = manager.Acquire();

        // Assert
        Assert.Equal(ApartmentState.MTA, rs.ApartmentState);

        // Cleanup
        manager.Release(rs);
    }

    [Fact]
    public void Constructor_WithCustomInitialSessionState_UsesProvidedState()
    {
        // Arrange
        var iss = InitialSessionState.CreateDefault();
        iss.Variables.Add(new SessionStateVariableEntry("TestVar", "TestValue", "Test variable"));

        // Act
        using var manager = new KestrunRunspacePoolManager(_host, 1, 2, iss);
        var rs = manager.Acquire();

        // Assert - Verify custom variable is available
        var testVar = rs.SessionStateProxy.GetVariable("TestVar");
        Assert.Equal("TestValue", testVar);

        // Cleanup
        manager.Release(rs);
    }

    [Fact]
    public void Constructor_PreWarmCreatesMinRunspaces()
    {
        // Arrange & Act - Constructor should pre-warm with min runspaces
        using var manager = new KestrunRunspacePoolManager(_host, 3, 5);

        // Assert - Acquiring should reuse pre-warmed runspaces immediately
        var rs1 = manager.Acquire();
        var rs2 = manager.Acquire();
        var rs3 = manager.Acquire();

        Assert.NotNull(rs1);
        Assert.NotNull(rs2);
        Assert.NotNull(rs3);

        // Cleanup
        manager.Release(rs1);
        manager.Release(rs2);
        manager.Release(rs3);
    }
}
