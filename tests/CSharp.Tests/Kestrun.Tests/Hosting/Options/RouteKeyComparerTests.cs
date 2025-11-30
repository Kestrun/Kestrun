using Kestrun.Hosting.Options;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class RouteKeyComparerTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", HttpVerb.Get);
        var y = ("/api/users", HttpVerb.Get);

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_DifferentPattern_ReturnsFalse()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", HttpVerb.Get);
        var y = ("/api/posts", HttpVerb.Get);

        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_DifferentMethod_ReturnsFalse()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", HttpVerb.Get);
        var y = ("/api/users", HttpVerb.Post);

        Assert.False(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Equals_CaseInsensitive_ReturnsTrue()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", HttpVerb.Get);
        var y = ("/API/USERS", HttpVerb.Get);

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", HttpVerb.Get);
        var y = ("/api/users", HttpVerb.Get);

        Assert.Equal(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_CaseInsensitive_ReturnsSameHash()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", HttpVerb.Get);
        var y = ("/API/USERS", HttpVerb.Get);

        Assert.Equal(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        var comparer = new RouteKeyComparer();
        var x = ("/api/users", HttpVerb.Get);
        var y = ("/api/posts", HttpVerb.Post);

        Assert.NotEqual(comparer.GetHashCode(x), comparer.GetHashCode(y));
    }
}
