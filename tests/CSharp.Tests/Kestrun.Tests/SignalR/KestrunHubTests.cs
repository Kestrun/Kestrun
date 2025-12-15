using Kestrun.SignalR;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Serilog;
using Xunit;

namespace KestrunTests.SignalR;

/// <summary>
/// Comprehensive tests for KestrunHub.
/// </summary>
[Trait("Category", "SignalR")]
public class KestrunHubTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IConnectionTracker> _mockTracker;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockClientProxy;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly KestrunHub _hub;

    public KestrunHubTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockTracker = new Mock<IConnectionTracker>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<ISingleClientProxy>();
        _mockGroups = new Mock<IGroupManager>();

        _hub = new KestrunHub(_mockLogger.Object, _mockTracker.Object)
        {
            Context = _mockContext.Object,
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object
        };

        // Setup default context behavior
        _ = _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        _ = _mockClients.Setup(c => c.Caller).Returns(_mockClientProxy.Object);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var hub = new KestrunHub(_mockLogger.Object, _mockTracker.Object);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public async Task OnConnectedAsync_LogsConnectionAndTracksClient()
    {
        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Information(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);

        _mockTracker.Verify(
            t => t.OnConnected("test-connection-id"),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithNoException_LogsNormalDisconnection()
    {
        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockLogger.Verify(
            l => l.Information(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);

        _mockTracker.Verify(
            t => t.OnDisconnected("test-connection-id"),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_LogsWarningWithException()
    {
        // Arrange
        var exception = new Exception("Test exception");

        // Act
        await _hub.OnDisconnectedAsync(exception);

        // Assert
        _mockLogger.Verify(
            l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);

        _mockTracker.Verify(
            t => t.OnDisconnected("test-connection-id"),
            Times.Once);
    }

    [Fact]
    public async Task JoinGroup_AddsClientToGroupAndLogsAction()
    {
        // Arrange
        var groupName = "TestGroup";
        _ = _mockGroups
            .Setup(g => g.AddToGroupAsync("test-connection-id", groupName, default))
            .Returns(Task.CompletedTask);

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("GroupJoined", It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinGroup(groupName);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", groupName, default),
            Times.Once);

        _mockLogger.Verify(
            l => l.Information(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);

        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "GroupJoined",
                It.Is<object[]>(args => args.Length == 1 && args[0].Equals(groupName)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task LeaveGroup_RemovesClientFromGroupAndLogsAction()
    {
        // Arrange
        var groupName = "TestGroup";
        _ = _mockGroups
            .Setup(g => g.RemoveFromGroupAsync("test-connection-id", groupName, default))
            .Returns(Task.CompletedTask);

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("GroupLeft", It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.LeaveGroup(groupName);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", groupName, default),
            Times.Once);

        _mockLogger.Verify(
            l => l.Information(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);

        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "GroupLeft",
                It.Is<object[]>(args => args.Length == 1 && args[0].Equals(groupName)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task Echo_SendsMessageBackToCaller()
    {
        // Arrange
        var message = "Test message";
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEcho", It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.Echo(message);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "ReceiveEcho",
                It.Is<object[]>(args => args.Length == 1 && args[0].Equals(message)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task JoinGroup_WithMultipleGroups_AddsToEachGroup()
    {
        // Arrange
        var groups = new[] { "Group1", "Group2", "Group3" };
        _ = _mockGroups
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        foreach (var group in groups)
        {
            await _hub.JoinGroup(group);
        }

        // Assert
        foreach (var group in groups)
        {
            _mockGroups.Verify(
                g => g.AddToGroupAsync("test-connection-id", group, default),
                Times.Once);
        }

        _mockLogger.Verify(
            l => l.Information(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task LeaveGroup_WithMultipleGroups_RemovesFromEachGroup()
    {
        // Arrange
        var groups = new[] { "Group1", "Group2", "Group3" };
        _ = _mockGroups
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        foreach (var group in groups)
        {
            await _hub.LeaveGroup(group);
        }

        // Assert
        foreach (var group in groups)
        {
            _mockGroups.Verify(
                g => g.RemoveFromGroupAsync("test-connection-id", group, default),
                Times.Once);
        }

        _mockLogger.Verify(
            l => l.Information(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Echo_WithEmptyMessage_SendsEmptyMessage()
    {
        // Arrange
        var message = "";
        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("ReceiveEcho", It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.Echo(message);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "ReceiveEcho",
                It.Is<object[]>(args => args.Length == 1 && args[0].Equals("")),
                default),
            Times.Once);
    }

    [Fact]
    public async Task ConnectionLifecycle_ConnectAndDisconnect_TracksCorrectly()
    {
        // Arrange
        var sequence = new MockSequence();
        _ = _mockTracker.InSequence(sequence).Setup(t => t.OnConnected("test-connection-id"));
        _ = _mockTracker.InSequence(sequence).Setup(t => t.OnDisconnected("test-connection-id"));

        // Act
        await _hub.OnConnectedAsync();
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockTracker.Verify(t => t.OnConnected("test-connection-id"), Times.Once);
        _mockTracker.Verify(t => t.OnDisconnected("test-connection-id"), Times.Once);
    }

    [Fact]
    public async Task JoinGroup_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var groupName = "Group-With_Special.Characters@123";
        _ = _mockGroups
            .Setup(g => g.AddToGroupAsync("test-connection-id", groupName, default))
            .Returns(Task.CompletedTask);

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("GroupJoined", It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinGroup(groupName);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", groupName, default),
            Times.Once);
    }

    [Fact]
    public async Task LeaveGroup_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var groupName = "Group-With_Special.Characters@123";
        _ = _mockGroups
            .Setup(g => g.RemoveFromGroupAsync("test-connection-id", groupName, default))
            .Returns(Task.CompletedTask);

        _ = _mockClientProxy
            .Setup(c => c.SendCoreAsync("GroupLeft", It.IsAny<object[]>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.LeaveGroup(groupName);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", groupName, default),
            Times.Once);
    }
}
