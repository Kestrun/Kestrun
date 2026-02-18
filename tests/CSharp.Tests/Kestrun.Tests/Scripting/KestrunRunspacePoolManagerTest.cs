using Kestrun.Scripting;
using Kestrun.Hosting;
using Xunit;


namespace KestrunTests.Scripting;

[Collection("RunspaceSerial")]
public class KestrunRunspacePoolManagerTest
{
    [Fact]
    [Trait("Category", "Scripting")]
    public void MaxRunspaces_ReturnsConfiguredMax()
    {
        // Arrange
        var minRunspaces = 1;
        var maxRunspaces = 5;
        using var host = new KestrunHost("Tests", Serilog.Log.Logger);
        var manager = new KestrunRunspacePoolManager(host, minRunspaces, maxRunspaces);

        // Act
        var actualMax = manager.MaxRunspaces;

        // Assert
        Assert.Equal(maxRunspaces, actualMax);
    }
}
