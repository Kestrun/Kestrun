namespace Kestrun.Hosting.Options;


internal class AuthKeyComparer : IEqualityComparer<(string Scheme, string Type)>
{
    private static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    public bool Equals((string Scheme, string Type) x, (string Scheme, string Type) y) => comparer.Equals(x.Scheme, y.Scheme) && comparer.Equals(x.Type, y.Type);

    public int GetHashCode((string Scheme, string Type) obj)
    {
        return HashCode.Combine(
            comparer.GetHashCode(obj.Scheme),
            comparer.GetHashCode(obj.Type));
    }
}
