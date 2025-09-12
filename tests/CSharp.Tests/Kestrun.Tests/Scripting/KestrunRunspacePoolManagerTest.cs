using Kestrun.Scripting;
using Xunit;


namespace KestrunTests.Scripting;

public class KestrunRunspacePoolManagerTest
{
    [Fact]
    [Trait("Category", "Scripting")]
    public void MaxRunspaces_ReturnsConfiguredMax()
    {
        // Arrange
        var minRunspaces = 1;
        var maxRunspaces = 5;
        var manager = new KestrunRunspacePoolManager(minRunspaces, maxRunspaces);

        // Act
        var actualMax = manager.MaxRunspaces;

        // Assert
        Assert.Equal(maxRunspaces, actualMax);
    }
}
