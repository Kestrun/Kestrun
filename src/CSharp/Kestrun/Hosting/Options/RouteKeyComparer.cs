using Kestrun.Utilities;

namespace Kestrun.Hosting.Options;

internal class RouteKeyComparer : IEqualityComparer<(string Pattern, HttpVerb Method)>
{
    private static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    public bool Equals((string Pattern, HttpVerb Method) x, (string Pattern, HttpVerb Method) y) => comparer.Equals(x.Pattern, y.Pattern) && comparer.Equals(x.Method, y.Method);
    public int GetHashCode((string Pattern, HttpVerb Method) obj)
    {
        return HashCode.Combine(
            comparer.GetHashCode(obj.Pattern),
            comparer.GetHashCode(obj.Method));
    }
}
