using Kestrun.SignalR;
using Xunit;

namespace KestrunTests.SignalR;

/// <summary>
/// Tests for SignalR integration infrastructure.
/// </summary>
public class SignalRInfrastructureTests
{
    [Fact]
    public void KestrunHub_CanBeInstantiated()
    {
        // Arrange & Act
        var hub = new KestrunHub(Serilog.Log.Logger);

        // Assert
        Assert.NotNull(hub);
    }

    [Fact]
    public void IRealtimeBroadcaster_InterfaceExists()
    {
        // Arrange & Act
        var interfaceType = typeof(IRealtimeBroadcaster);

        // Assert
        Assert.NotNull(interfaceType);
        Assert.True(interfaceType.IsInterface);
        
        // Verify interface has expected methods
        var methods = interfaceType.GetMethods();
        Assert.Contains(methods, m => m.Name == "BroadcastLogAsync");
        Assert.Contains(methods, m => m.Name == "BroadcastEventAsync");
        Assert.Contains(methods, m => m.Name == "BroadcastToGroupAsync");
    }

    [Fact]
    public void RealtimeBroadcaster_ImplementsInterface()
    {
        // Arrange & Act
        var broadcasterType = typeof(RealtimeBroadcaster);

        // Assert
        Assert.True(typeof(IRealtimeBroadcaster).IsAssignableFrom(broadcasterType));
    }
}
