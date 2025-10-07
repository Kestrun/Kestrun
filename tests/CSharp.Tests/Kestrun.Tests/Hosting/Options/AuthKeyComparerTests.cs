using Kestrun.Hosting.Options;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class AuthKeyComparerTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var comparer = new AuthKeyComparer();
        var x = ("Bearer", "JWT");
        var y = ("Bearer", "JWT");
        
        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_DifferentScheme_ReturnsFalse()
    {
        var comparer = new AuthKeyComparer();
        var x = ("Bearer", "JWT");
        var y = ("Basic", "JWT");
        
        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var comparer = new AuthKeyComparer();
        var x = ("Bearer", "JWT");
        var y = ("Bearer", "ApiKey");
        
        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_CaseInsensitive_ReturnsTrue()
    {
        var comparer = new AuthKeyComparer();
        var x = ("Bearer", "JWT");
        var y = ("bearer", "jwt");
        
        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        var comparer = new AuthKeyComparer();
        var x = ("Bearer", "JWT");
        var y = ("Bearer", "JWT");
        
        Assert.Equal(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_CaseInsensitive_ReturnsSameHash()
    {
        var comparer = new AuthKeyComparer();
        var x = ("Bearer", "JWT");
        var y = ("bearer", "jwt");
        
        Assert.Equal(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        var comparer = new AuthKeyComparer();
        var x = ("Bearer", "JWT");
        var y = ("Basic", "ApiKey");
        
        Assert.NotEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }
}
