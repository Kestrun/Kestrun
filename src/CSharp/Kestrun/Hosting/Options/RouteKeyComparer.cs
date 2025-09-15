namespace Kestrun.Hosting.Options;


internal class RouteKeyComparer : IEqualityComparer<(string Pattern, string Method)>
{
    private static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    public bool Equals((string Pattern, string Method) x, (string Pattern, string Method) y) => comparer.Equals(x.Pattern, y.Pattern) && comparer.Equals(x.Method, y.Method);

    public int GetHashCode((string Pattern, string Method) obj)
    {
        return HashCode.Combine(
            comparer.GetHashCode(obj.Pattern),
            comparer.GetHashCode(obj.Method));
    }
}
