using Kestrun.Utilities;
using Xunit;

namespace Kestrun.Tests.Utilities;

public class KestrunLockRegistryTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void GetOrCreate_NormalizesKeyAndReturnsSameSemaphore()
    {
        SemaphoreSlim first = KestrunLockRegistry.GetOrCreate(" Bike-State ");
        SemaphoreSlim second = KestrunLockRegistry.GetOrCreate("bike-state");
        SemaphoreSlim third = KestrunLockRegistry.GetOrCreate("BIKE-STATE");

        Assert.Same(first, second);
        Assert.Same(first, third);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void TryGet_AfterCreate_ReturnsExistingSemaphore()
    {
        SemaphoreSlim created = KestrunLockRegistry.GetOrCreate("catalog-sync");

        bool found = KestrunLockRegistry.TryGet("  CATALOG-SYNC  ", out SemaphoreSlim? existing);

        Assert.True(found);
        Assert.NotNull(existing);
        Assert.Same(created, existing);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void GetOrCreate_DifferentKeys_ReturnDifferentSemaphores()
    {
        SemaphoreSlim first = KestrunLockRegistry.GetOrCreate("inventory-import");
        SemaphoreSlim second = KestrunLockRegistry.GetOrCreate("inventory-export");

        Assert.NotSame(first, second);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void Default_ReturnsDefaultRegistrySemaphore()
    {
        SemaphoreSlim byProperty = KestrunLockRegistry.Default;
        SemaphoreSlim byKey = KestrunLockRegistry.GetOrCreate("default");

        Assert.Same(byProperty, byKey);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void GetOrCreate_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => KestrunLockRegistry.GetOrCreate(null!));
    }

    [Theory]
    [Trait("Category", "Utilities")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void GetOrCreate_WhitespaceKey_ThrowsArgumentException(string key)
    {
        Assert.Throws<ArgumentException>(() => KestrunLockRegistry.GetOrCreate(key));
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void TryGet_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => KestrunLockRegistry.TryGet(null!, out _));
    }

    [Theory]
    [Trait("Category", "Utilities")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void TryGet_WhitespaceKey_ThrowsArgumentException(string key)
    {
        Assert.Throws<ArgumentException>(() => KestrunLockRegistry.TryGet(key, out _));
    }
}
