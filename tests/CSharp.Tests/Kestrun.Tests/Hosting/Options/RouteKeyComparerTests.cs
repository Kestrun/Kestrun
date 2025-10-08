using Kestrun.Hosting.Options;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class RouteKeyComparerTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", "GET");
        var y = ("/api/users", "GET");

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_DifferentPattern_ReturnsFalse()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", "GET");
        var y = ("/api/posts", "GET");

        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_DifferentMethod_ReturnsFalse()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", "GET");
        var y = ("/api/users", "POST");

        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_CaseInsensitive_ReturnsTrue()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", "GET");
        var y = ("/API/USERS", "get");

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", "GET");
        var y = ("/api/users", "GET");

        Assert.Equal(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_CaseInsensitive_ReturnsSameHash()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", "GET");
        var y = ("/API/USERS", "get");

        Assert.Equal(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", "GET");
        var y = ("/api/posts", "POST");

        Assert.NotEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }
}
