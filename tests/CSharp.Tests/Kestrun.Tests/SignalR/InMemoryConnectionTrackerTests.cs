using Kestrun.SignalR;
using Xunit;

namespace KestrunTests.SignalR;

/// <summary>
/// Tests for InMemoryConnectionTracker.
/// </summary>
[Trait("Category", "SignalR")]
public class InMemoryConnectionTrackerTests
{
    [Fact]
    public void Constructor_InitializesWithZeroConnections()
    {
        // Arrange & Act
        var tracker = new InMemoryConnectionTracker();

        // Assert
        Assert.Equal(0, tracker.ConnectedCount);
    }

    [Fact]
    public void OnConnected_AddsConnection()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act
        tracker.OnConnected("conn1");

        // Assert
        Assert.Equal(1, tracker.ConnectedCount);
    }

    [Fact]
    public void OnConnected_MultipleConnections_IncrementsCount()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act
        tracker.OnConnected("conn1");
        tracker.OnConnected("conn2");
        tracker.OnConnected("conn3");

        // Assert
        Assert.Equal(3, tracker.ConnectedCount);
    }

    [Fact]
    public void OnConnected_DuplicateConnectionId_OverwritesExisting()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act
        tracker.OnConnected("conn1");
        tracker.OnConnected("conn1"); // Same ID

        // Assert
        Assert.Equal(1, tracker.ConnectedCount);
    }

    [Fact]
    public void OnConnected_WithNullConnectionId_DoesNotAdd()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act
        tracker.OnConnected(null!);

        // Assert
        Assert.Equal(0, tracker.ConnectedCount);
    }

    [Fact]
    public void OnConnected_WithEmptyConnectionId_DoesNotAdd()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act
        tracker.OnConnected("");

        // Assert
        Assert.Equal(0, tracker.ConnectedCount);
    }

    [Fact]
    public void OnDisconnected_RemovesConnection()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");

        // Act
        tracker.OnDisconnected("conn1");

        // Assert
        Assert.Equal(0, tracker.ConnectedCount);
    }

    [Fact]
    public void OnDisconnected_NonExistentConnection_DoesNotAffectCount()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");

        // Act
        tracker.OnDisconnected("conn2"); // Different ID

        // Assert
        Assert.Equal(1, tracker.ConnectedCount);
    }

    [Fact]
    public void OnDisconnected_WithNullConnectionId_DoesNotThrow()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");

        // Act & Assert (no exception)
        tracker.OnDisconnected(null!);
        Assert.Equal(1, tracker.ConnectedCount);
    }

    [Fact]
    public void OnDisconnected_WithEmptyConnectionId_DoesNotThrow()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");

        // Act & Assert (no exception)
        tracker.OnDisconnected("");
        Assert.Equal(1, tracker.ConnectedCount);
    }

    [Fact]
    public void GetConnections_WithNoConnections_ReturnsEmptyCollection()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act
        var connections = tracker.GetConnections();

        // Assert
        Assert.NotNull(connections);
        Assert.Empty(connections);
    }

    [Fact]
    public void GetConnections_WithConnections_ReturnsAllConnectionIds()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");
        tracker.OnConnected("conn2");
        tracker.OnConnected("conn3");

        // Act
        var connections = tracker.GetConnections();

        // Assert
        Assert.Equal(3, connections.Count);
        Assert.Contains("conn1", connections);
        Assert.Contains("conn2", connections);
        Assert.Contains("conn3", connections);
    }

    [Fact]
    public void GetConnections_ReturnsSnapshot_NotLiveCollection()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");
        var snapshot1 = tracker.GetConnections();

        // Act
        tracker.OnConnected("conn2");
        var snapshot2 = tracker.GetConnections();

        // Assert
        _ = Assert.Single(snapshot1); // Original snapshot unchanged
        Assert.Equal(2, snapshot2.Count); // New snapshot has both
    }

    [Fact]
    public void GetConnections_AfterDisconnection_ReflectsCurrentState()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");
        tracker.OnConnected("conn2");
        tracker.OnDisconnected("conn1");

        // Act
        var connections = tracker.GetConnections();

        // Assert
        _ = Assert.Single(connections);
        Assert.Contains("conn2", connections);
        Assert.DoesNotContain("conn1", connections);
    }

    [Fact]
    public void ConnectedCount_ReflectsCurrentConnectionCount()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act & Assert
        Assert.Equal(0, tracker.ConnectedCount);

        tracker.OnConnected("conn1");
        Assert.Equal(1, tracker.ConnectedCount);

        tracker.OnConnected("conn2");
        Assert.Equal(2, tracker.ConnectedCount);

        tracker.OnDisconnected("conn1");
        Assert.Equal(1, tracker.ConnectedCount);

        tracker.OnDisconnected("conn2");
        Assert.Equal(0, tracker.ConnectedCount);
    }

    [Fact]
    public async Task ThreadSafety_MultipleOperations_MaintainsConsistency()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        var tasks = new List<Task>();

        // Act - Simulate concurrent connections
        for (var i = 0; i < 100; i++)
        {
            var connId = $"conn{i}";
            tasks.Add(Task.Run(() => tracker.OnConnected(connId)));
        }

        await Task.WhenAll([.. tasks]);

        // Assert
        Assert.Equal(100, tracker.ConnectedCount);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentConnectDisconnect_MaintainsConsistency()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        var connectTasks = new List<Task>();
        var disconnectTasks = new List<Task>();

        // Act - First, connect 50 in parallel
        for (var i = 0; i < 50; i++)
        {
            var connId = $"conn{i}";
            connectTasks.Add(Task.Run(() => tracker.OnConnected(connId)));
        }

        // Wait for all connections to complete
        await Task.WhenAll([.. connectTasks]);

        // Then disconnect 25 in parallel
        for (var i = 0; i < 25; i++)
        {
            var connId = $"conn{i}";
            disconnectTasks.Add(Task.Run(() => tracker.OnDisconnected(connId)));
        }

        await Task.WhenAll([.. disconnectTasks]);

        // Assert - Should have exactly 25 remaining connections
        Assert.Equal(25, tracker.ConnectedCount);
    }

    [Fact]
    public void GetConnections_ReturnsReadOnlyCollection()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();
        tracker.OnConnected("conn1");

        // Act
        var connections = tracker.GetConnections();

        // Assert
        _ = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(connections);
    }

    [Fact]
    public void OnConnected_WithWhitespaceConnectionId_AddsConnection()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act
        tracker.OnConnected("   ");

        // Assert - Implementation uses IsNullOrEmpty, so whitespace is valid
        Assert.Equal(1, tracker.ConnectedCount);
    }

    [Fact]
    public void ConnectionLifecycle_CompleteFlow_WorksCorrectly()
    {
        // Arrange
        var tracker = new InMemoryConnectionTracker();

        // Act & Assert - Empty state
        Assert.Equal(0, tracker.ConnectedCount);
        Assert.Empty(tracker.GetConnections());

        // Connect multiple clients
        tracker.OnConnected("client1");
        tracker.OnConnected("client2");
        tracker.OnConnected("client3");
        Assert.Equal(3, tracker.ConnectedCount);

        // Verify all present
        var connections = tracker.GetConnections();
        Assert.Contains("client1", connections);
        Assert.Contains("client2", connections);
        Assert.Contains("client3", connections);

        // Disconnect one
        tracker.OnDisconnected("client2");
        Assert.Equal(2, tracker.ConnectedCount);
        connections = tracker.GetConnections();
        Assert.DoesNotContain("client2", connections);

        // Disconnect all remaining
        tracker.OnDisconnected("client1");
        tracker.OnDisconnected("client3");
        Assert.Equal(0, tracker.ConnectedCount);
        Assert.Empty(tracker.GetConnections());
    }
}
