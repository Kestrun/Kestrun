using System.Text;

namespace Kestrun;

/// <summary>
/// Provides constant-time comparison methods to prevent timing attacks.
/// </summary>
public static class FixedTimeEquals
{
    /// <summary>
    /// Compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">First byte array.</param>
    /// <param name="b">Second byte array.</param>
    /// <returns>True if both arrays are equal, false otherwise.</returns>  
    public static bool Test(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }

    /// <summary>
    /// Compares two strings in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">First string to compare.</param>
    /// <param name="b">Second string to compare.</param>
    /// <returns>True if both strings are equal, false otherwise.</returns>
    public static bool Test(string? a, string? b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return Test(aBytes, bBytes);
    }

    /// <summary>
    /// Compares a byte span and a string in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">The byte span to compare.</param>
    /// <param name="b">The string to compare.</param>
    /// <returns>True if both are equal, false otherwise.</returns>
    public static bool Test(ReadOnlySpan<byte> a, string? b)
    {
        if (b == null)
        {
            return false;
        }

        var bBytes = Encoding.UTF8.GetBytes(b);
        return Test(a, bBytes);
    }
    /// <summary>
    /// Compares a string and a byte span in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">The string to compare.</param>
    /// <param name="b">The byte span to compare.</param>
    /// <returns>True if both are equal, false otherwise.</returns>
    public static bool Test(string? a, ReadOnlySpan<byte> b)
    {
        if (a == null)
        {
            return false;
        }

        var aBytes = Encoding.UTF8.GetBytes(a);
        return Test(aBytes, b);
    }

    /// <summary>
    /// Compares a byte array and a string in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">The byte array to compare.</param>
    /// <param name="b">The string to compare.</param>
    /// <returns>True if both are equal, false otherwise.</returns>
    public static bool Test(byte[] a, string b) =>
    Test(a.AsSpan(), b);

    /// <summary>
    /// Compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">First byte array.</param>
    /// <param name="b">Second byte array.</param>
    /// <returns>True if both arrays are equal, false otherwise.</returns>
    public static bool Test(byte[] a, byte[] b) =>
    Test(a.AsSpan(), b.AsSpan());

    /// <summary>
    /// Compares a string and a byte array in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">The string to compare.</param>
    /// <param name="b">The byte array to compare.</param>
    /// <returns>True if both are equal, false otherwise.</returns>
    public static bool Test(string? a, byte[] b) =>
    Test(a, b.AsSpan());
}
