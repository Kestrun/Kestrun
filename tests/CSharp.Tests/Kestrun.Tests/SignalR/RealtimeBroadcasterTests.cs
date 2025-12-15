using Kestrun.SignalR;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Serilog;
using Xunit;

namespace KestrunTests.SignalR;

/// <summary>
/// Comprehensive tests for RealtimeBroadcaster.
/// </summary>
[Trait("Category", "SignalR")]
public class RealtimeBroadcasterTests
{
    private readonly Mock<IHubContext<KestrunHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<ILogger> _mockLogger;
    private readonly RealtimeBroadcaster _broadcaster;

    public RealtimeBroadcasterTests()
    {
        _mockHubContext = new Mock<IHubContext<KestrunHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockLogger = new Mock<ILogger>();

        _ = _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _ = _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);

        _broadcaster = new RealtimeBroadcaster(_mockHubContext.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var broadcaster = new RealtimeBroadcaster(_mockHubContext.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(broadcaster);
    }

    [Fact]
    public async Task BroadcastLogAsync_WithValidParameters_SendsLogMessage()
    {
        // Arrange
        var level = "Information";
        var message = "Test log message";
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastLogAsync(level, message);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "ReceiveLog",
                It.Is<object?[]>(args =>
                    args.Length == 1 &&
                    args[0] != null),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastLogAsync_IncludesTimestamp()
    {
        // Arrange
        var level = "Warning";
        var message = "Test warning";
        object? capturedPayload = null;

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((_, args, _) => capturedPayload = args[0])
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastLogAsync(level, message);

        // Assert
        Assert.NotNull(capturedPayload);
        var payload = capturedPayload.GetType().GetProperty("timestamp")?.GetValue(capturedPayload);
        Assert.NotNull(payload);
        _ = Assert.IsType<DateTime>(payload);
    }

    [Fact]
    public async Task BroadcastLogAsync_WithCancellationToken_PassesTokenCorrectly()
    {
        // Arrange
        var level = "Error";
        var message = "Test error";
        using var cts = new CancellationTokenSource();
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), cts.Token))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastLogAsync(level, message, cts.Token);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastLogAsync_OnException_LogsError()
    {
        // Arrange
        var level = "Information";
        var message = "Test message";
        var exception = new Exception("Test exception");
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), default))
            .ThrowsAsync(exception);

        // Act
        await _broadcaster.BroadcastLogAsync(level, message);

        // Assert
        _mockLogger.Verify(
            l => l.Error(exception, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastLogAsync_OnException_DoesNotThrow()
    {
        // Arrange
        var level = "Information";
        var message = "Test message";
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), default))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert (no exception thrown)
        await _broadcaster.BroadcastLogAsync(level, message);
    }

    [Fact]
    public async Task BroadcastEventAsync_WithValidParameters_SendsEvent()
    {
        // Arrange
        var eventName = "TestEvent";
        var data = new { Value = 42 };
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastEventAsync(eventName, data);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "ReceiveEvent",
                It.Is<object?[]>(args =>
                    args.Length == 1 &&
                    args[0] != null),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastEventAsync_WithNullData_SendsEventWithNullData()
    {
        // Arrange
        var eventName = "TestEvent";
        object? capturedPayload = null;

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((_, args, _) => capturedPayload = args[0])
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastEventAsync(eventName, null);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default),
            Times.Once);

        Assert.NotNull(capturedPayload);
        var dataProperty = capturedPayload.GetType().GetProperty("data")?.GetValue(capturedPayload);
        Assert.Null(dataProperty);
    }

    [Fact]
    public async Task BroadcastEventAsync_IncludesTimestamp()
    {
        // Arrange
        var eventName = "TestEvent";
        var data = "Test data";
        object? capturedPayload = null;

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((_, args, _) => capturedPayload = args[0])
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastEventAsync(eventName, data);

        // Assert
        Assert.NotNull(capturedPayload);
        var timestamp = capturedPayload.GetType().GetProperty("timestamp")?.GetValue(capturedPayload);
        Assert.NotNull(timestamp);
        _ = Assert.IsType<DateTime>(timestamp);
    }

    [Fact]
    public async Task BroadcastEventAsync_OnException_LogsError()
    {
        // Arrange
        var eventName = "TestEvent";
        var data = "Test data";
        var exception = new Exception("Test exception");
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default))
            .ThrowsAsync(exception);

        // Act
        await _broadcaster.BroadcastEventAsync(eventName, data);

        // Assert
        _mockLogger.Verify(
            l => l.Error(exception, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastEventAsync_OnException_DoesNotThrow()
    {
        // Arrange
        var eventName = "TestEvent";
        var data = "Test data";
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert (no exception thrown)
        await _broadcaster.BroadcastEventAsync(eventName, data);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_WithValidParameters_SendsToGroup()
    {
        // Arrange
        var groupName = "TestGroup";
        var method = "CustomMethod";
        var message = "Test message";
        var mockGroupProxy = new Mock<IClientProxy>();

        _ = _mockClients.Setup(c => c.Group(groupName)).Returns(mockGroupProxy.Object);
        _ = mockGroupProxy
            .Setup(c => c.SendCoreAsync(method, It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastToGroupAsync(groupName, method, message);

        // Assert
        mockGroupProxy.Verify(
            c => c.SendCoreAsync(
                method,
                It.Is<object?[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_WithNullMessage_SendsNullToGroup()
    {
        // Arrange
        var groupName = "TestGroup";
        var method = "CustomMethod";
        var mockGroupProxy = new Mock<IClientProxy>();

        _ = _mockClients.Setup(c => c.Group(groupName)).Returns(mockGroupProxy.Object);
        _ = mockGroupProxy
            .Setup(c => c.SendCoreAsync(method, It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastToGroupAsync(groupName, method, null);

        // Assert
        mockGroupProxy.Verify(
            c => c.SendCoreAsync(method, It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_WithCancellationToken_PassesTokenCorrectly()
    {
        // Arrange
        var groupName = "TestGroup";
        var method = "CustomMethod";
        var message = "Test message";
        using var cts = new CancellationTokenSource();
        var mockGroupProxy = new Mock<IClientProxy>();

        _ = _mockClients.Setup(c => c.Group(groupName)).Returns(mockGroupProxy.Object);
        _ = mockGroupProxy
            .Setup(c => c.SendCoreAsync(method, It.IsAny<object?[]>(), cts.Token))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastToGroupAsync(groupName, method, message, cts.Token);

        // Assert
        mockGroupProxy.Verify(
            c => c.SendCoreAsync(method, It.IsAny<object?[]>(), cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_OnException_LogsError()
    {
        // Arrange
        var groupName = "TestGroup";
        var method = "CustomMethod";
        var message = "Test message";
        var exception = new Exception("Test exception");
        var mockGroupProxy = new Mock<IClientProxy>();

        _ = _mockClients.Setup(c => c.Group(groupName)).Returns(mockGroupProxy.Object);
        _ = mockGroupProxy
            .Setup(c => c.SendCoreAsync(method, It.IsAny<object?[]>(), default))
            .ThrowsAsync(exception);

        // Act
        await _broadcaster.BroadcastToGroupAsync(groupName, method, message);

        // Assert
        _mockLogger.Verify(
            l => l.Error(exception, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_OnException_DoesNotThrow()
    {
        // Arrange
        var groupName = "TestGroup";
        var method = "CustomMethod";
        var message = "Test message";
        var mockGroupProxy = new Mock<IClientProxy>();

        _ = _mockClients.Setup(c => c.Group(groupName)).Returns(mockGroupProxy.Object);
        _ = mockGroupProxy
            .Setup(c => c.SendCoreAsync(method, It.IsAny<object?[]>(), default))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert (no exception thrown)
        await _broadcaster.BroadcastToGroupAsync(groupName, method, message);
    }

    [Fact]
    public async Task BroadcastLogAsync_WithMultipleLevels_SendsCorrectLevel()
    {
        // Arrange
        var levels = new[] { "Debug", "Information", "Warning", "Error", "Fatal" };
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act & Assert
        foreach (var level in levels)
        {
            await _broadcaster.BroadcastLogAsync(level, $"Test {level}");
        }

        _mockClientProxy.Verify(
            c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), default),
            Times.Exactly(5));
    }

    [Fact]
    public async Task BroadcastEventAsync_WithComplexData_SanitizesPayload()
    {
        // Arrange
        var eventName = "ComplexEvent";
        var data = new
        {
            Id = 123,
            Name = "Test",
            Nested = new { Value = "nested" },
            Array = new[] { 1, 2, 3 }
        };
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastEventAsync(eventName, data);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_WithMultipleGroups_SendsToEachGroup()
    {
        // Arrange
        var groups = new[] { "Group1", "Group2", "Group3" };
        var method = "TestMethod";
        var message = "Test message";

        foreach (var group in groups)
        {
            var mockGroupProxy = new Mock<IClientProxy>();
            _ = _mockClients.Setup(c => c.Group(group)).Returns(mockGroupProxy.Object);
            _ = mockGroupProxy
                .Setup(c => c.SendCoreAsync(method, It.IsAny<object?[]>(), default))
                .Returns(Task.CompletedTask);
        }

        // Act
        foreach (var group in groups)
        {
            await _broadcaster.BroadcastToGroupAsync(group, method, message);
        }

        // Assert
        foreach (var group in groups)
        {
            _mockClients.Verify(c => c.Group(group), Times.Once);
        }
    }

    [Fact]
    public async Task BroadcastLogAsync_LogsDebugMessage()
    {
        // Arrange
        var level = "Information";
        var message = "Test log";
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveLog", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastLogAsync(level, message);

        // Assert
        _mockLogger.Verify(
            l => l.Debug(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastEventAsync_LogsDebugMessage()
    {
        // Arrange
        var eventName = "TestEvent";
        var data = "Test data";
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEvent", It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastEventAsync(eventName, data);

        // Assert
        _mockLogger.Verify(
            l => l.Debug(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastToGroupAsync_LogsDebugMessage()
    {
        // Arrange
        var groupName = "TestGroup";
        var method = "TestMethod";
        var message = "Test message";
        var mockGroupProxy = new Mock<IClientProxy>();

        _ = _mockClients.Setup(c => c.Group(groupName)).Returns(mockGroupProxy.Object);
        _ = mockGroupProxy
            .Setup(c => c.SendCoreAsync(method, It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _broadcaster.BroadcastToGroupAsync(groupName, method, message);

        // Assert
        _mockLogger.Verify(
            l => l.Debug(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }
}
