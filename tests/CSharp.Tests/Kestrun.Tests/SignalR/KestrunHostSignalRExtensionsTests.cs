using Kestrun.Hosting;
using Kestrun.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Serilog;
using Xunit;

namespace KestrunTests.SignalR;

/// <summary>
/// Tests for KestrunHostSignalRExtensions methods.
/// </summary>
public class KestrunHostSignalRExtensionsTests
{
    private readonly Mock<IConnectionTracker> _mockConnectionTracker;
    private readonly Mock<IRealtimeBroadcaster> _mockBroadcaster;

    public KestrunHostSignalRExtensionsTests()
    {
        _mockConnectionTracker = new Mock<IConnectionTracker>();
        _mockBroadcaster = new Mock<IRealtimeBroadcaster>();
    }

    #region GetConnectedClientCount Tests

    [Fact]
    public void GetConnectedClientCount_ReturnsNullWhenHostAppIsNull()
    {
        // Arrange
        using var host = new KestrunHost("TestApp", Log.Logger);
        // Don't build the app, so App will be null

        // Act
        var result = host.GetConnectedClientCount();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetConnectedClientCount_ReturnsNullWhenTrackerNotRegistered()
    {
        // Arrange
        _ = _mockConnectionTracker.Setup(ct => ct.ConnectedCount).Returns(0);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            // Don't register tracker - so it won't be found
        });
        _ = host.Build();

        // Act
        var result = host.GetConnectedClientCount();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetConnectedClientCount_ReturnsCountWhenTrackerRegistered()
    {
        // Arrange
        _ = _mockConnectionTracker.Setup(ct => ct.ConnectedCount).Returns(5);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockConnectionTracker.Object);
        });
        _ = host.Build();

        // Act
        var result = host.GetConnectedClientCount();

        // Assert
        _ = Assert.NotNull(result);
        Assert.Equal(5, result);
    }

    [Fact]
    public void GetConnectedClientCount_ReturnsZeroWhenNoClientsConnected()
    {
        // Arrange
        _ = _mockConnectionTracker.Setup(ct => ct.ConnectedCount).Returns(0);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockConnectionTracker.Object);
        });
        _ = host.Build();

        // Act
        var result = host.GetConnectedClientCount();

        // Assert
        _ = Assert.NotNull(result);
        Assert.Equal(0, result);
    }

    #endregion

    #region BroadcastLogAsync Tests

    [Fact]
    public async Task BroadcastLogAsync_ReturnsNullWhenNoServiceProvider()
    {
        // Arrange
        using var host = new KestrunHost("TestApp", Log.Logger);

        // Act
        var result = await host.BroadcastLogAsync("Information", "Test message");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BroadcastLogAsync_ReturnsNullWhenBroadcasterNotRegistered()
    {
        // Arrange
        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            // Don't register broadcaster
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastLogAsync("Information", "Test message");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BroadcastLogAsync_SuccessfullySendsLog()
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastLogAsync("Warning", "Test warning", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastLogAsync("Warning", "Test warning");

        // Assert
        Assert.True(result);
        _mockBroadcaster.Verify(b => b.BroadcastLogAsync("Warning", "Test warning", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastLogAsync_HandlesBroadcasterException()
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastLogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("Broadcast failed"));

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastLogAsync("Error", "Test error");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("Information")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Debug")]
    public async Task BroadcastLogAsync_SupportsMultipleLevels(string level)
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastLogAsync(level, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastLogAsync(level, "Test message");

        // Assert
        Assert.True(result);
    }

    #endregion

    #region BroadcastEventAsync Tests

    [Fact]
    public async Task BroadcastEventAsync_ReturnsNullWhenNoServiceProvider()
    {
        // Arrange
        using var host = new KestrunHost("TestApp", Log.Logger);

        // Act
        var result = await host.BroadcastEventAsync("TestEvent", new { data = "test" });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BroadcastEventAsync_ReturnsNullWhenBroadcasterNotRegistered()
    {
        // Arrange
        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            // Don't register broadcaster
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastEventAsync("TestEvent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BroadcastEventAsync_SuccessfullyBroadcasts()
    {
        // Arrange
        var eventData = new { eventId = 456 };

        _ = _mockBroadcaster.Setup(b => b.BroadcastEventAsync("CustomEvent", eventData, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastEventAsync("CustomEvent", eventData);

        // Assert
        Assert.True(result);
        _mockBroadcaster.Verify(b => b.BroadcastEventAsync("CustomEvent", eventData, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastEventAsync_WithNullData_SuccessfullyBroadcasts()
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastEventAsync("SimpleEvent", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastEventAsync("SimpleEvent");

        // Assert
        Assert.True(result);
        _mockBroadcaster.Verify(b => b.BroadcastEventAsync("SimpleEvent", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastEventAsync_HandlesBroadcasterException()
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastEventAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("Broadcast failed"));

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastEventAsync("FailingEvent");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region BroadcastToGroupAsync Tests

    [Fact]
    public async Task BroadcastToGroupAsync_ReturnsNullWhenNoServiceProvider()
    {
        // Arrange
        using var host = new KestrunHost("TestApp", Log.Logger);

        // Act
        var result = await host.BroadcastToGroupAsync("TestGroup", "Method", "data");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_ReturnsNullWhenBroadcasterNotRegistered()
    {
        // Arrange
        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            // Don't register broadcaster
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastToGroupAsync("TestGroup", "Method", null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_SuccessfullyBroadcasts()
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastToGroupAsync("AdminGroup", "UpdateUsers", It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        var adminData = new { userId = 789 };

        // Act
        var result = await host.BroadcastToGroupAsync("AdminGroup", "UpdateUsers", adminData);

        // Assert
        Assert.True(result);
        _mockBroadcaster.Verify(b => b.BroadcastToGroupAsync("AdminGroup", "UpdateUsers", adminData, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_WithNullMessage_SuccessfullyBroadcasts()
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastToGroupAsync("NotifyGroup", "OnNotify", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastToGroupAsync("NotifyGroup", "OnNotify", null);

        // Assert
        Assert.True(result);
        _mockBroadcaster.Verify(b => b.BroadcastToGroupAsync("NotifyGroup", "OnNotify", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_HandlesBroadcasterException()
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Throws(new Exception("Group broadcast failed"));

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastToGroupAsync("FailingGroup", "Method", "data");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("Group1")]
    [InlineData("Group2")]
    [InlineData("AdminGroup")]
    public async Task BroadcastToGroupAsync_SupportsMultipleGroups(string groupName)
    {
        // Arrange
        _ = _mockBroadcaster.Setup(b => b.BroadcastToGroupAsync(groupName, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var host = new KestrunHost("TestApp", Log.Logger);
        _ = host.AddService(services =>
        {
            _ = services.AddSingleton(_mockBroadcaster.Object);
        });
        _ = host.Build();

        // Act
        var result = await host.BroadcastToGroupAsync(groupName, "Notify", "message");

        // Assert
        Assert.True(result);
    }

    #endregion
}
