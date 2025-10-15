using System.Runtime.CompilerServices;

namespace Kestrun.Utilities.Json;

/// <summary>
/// Equality comparer that compares object references instead of values.
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();

    bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
