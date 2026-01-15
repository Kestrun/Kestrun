using KestrunReferenceEqualityComparer = Kestrun.Utilities.Json.ReferenceEqualityComparer;
using Xunit;

namespace KestrunTests.Utilities;

public class ReferenceEqualityComparerTests
{
    private sealed class Box
    {
        public int Value { get; set; }
        public override bool Equals(object? obj) => obj is Box other && other.Value == Value;
        public override int GetHashCode() => Value.GetHashCode();
    }

    [Fact]
    public void Equals_Uses_Reference_Equality()
    {
        IEqualityComparer<object> comparer = KestrunReferenceEqualityComparer.Instance;

        var a1 = new Box { Value = 1 };
        var a2 = new Box { Value = 1 }; // value-equal but not same reference

        Assert.False(comparer.Equals(a1, a2));
        Assert.True(comparer.Equals(a1, a1));
    }

    [Fact]
    public void GetHashCode_Uses_RuntimeHelpers_Based_Hash()
    {
        IEqualityComparer<object> comparer = KestrunReferenceEqualityComparer.Instance;

        var a1 = new Box { Value = 1 };
        var a2 = new Box { Value = 1 };

        var h1 = comparer.GetHashCode(a1);
        var h2 = comparer.GetHashCode(a2);

        // Not guaranteed to differ, but extremely likely; the core guarantee we want is stability per reference.
        Assert.Equal(h1, comparer.GetHashCode(a1));
        Assert.Equal(h2, comparer.GetHashCode(a2));
    }
}
