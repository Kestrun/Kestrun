
using System.Runtime.InteropServices;
using System.Security;
using Serilog;

namespace Kestrun.Utilities;


/// <summary>
/// Provides utility methods for working with SecureString and ReadOnlySpan&lt;char&gt;.
/// </summary>
public static class SecureStringUtils
{
    /// <summary>
    /// Represents a delegate that handles a ReadOnlySpan&lt;char&gt;.
    /// </summary>
    public delegate void SpanHandler(ReadOnlySpan<char> span);


    /// <summary>
    /// Converts a SecureString to a ReadOnlySpan&lt;char&gt; and passes it to the specified handler.
    /// The unmanaged memory is zeroed and freed after the handler executes.
    /// </summary>
    /// <param name="secureString">The SecureString to convert.</param>
    /// <param name="handler">The delegate to handle the ReadOnlySpan&lt;char&gt;.</param>
    public static unsafe void ToSecureSpan(this SecureString secureString, SpanHandler handler)
    {
        Log.Debug("Converting SecureString to ReadOnlySpan<char> for handler {Handler}", handler.Method.Name);

        ArgumentNullException.ThrowIfNull(secureString);
        ArgumentNullException.ThrowIfNull(handler);
        if (secureString.Length == 0)
        {
            throw new ArgumentException("SecureString is empty", nameof(secureString));
        }
        // Convert SecureString to a ReadOnlySpan<char> using a pointer
        // This is safe because SecureString guarantees that the memory is zeroed after use.
        var ptr = IntPtr.Zero;
        try
        {
            // Convert SecureString to a pointer
            // Marshal.SecureStringToCoTaskMemUnicode returns a pointer to the unmanaged memory
            // that contains the characters of the SecureString.
            // This memory must be freed after use to avoid memory leaks.
            Log.Debug("Marshalling SecureString to unmanaged memory");
            ptr = Marshal.SecureStringToCoTaskMemUnicode(secureString);
            var span = new ReadOnlySpan<char>((char*)ptr, secureString.Length);
            handler(span);
            Log.Debug("Handler executed successfully with SecureString span");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while converting SecureString to ReadOnlySpan<char>");
            throw; // rethrow the exception for further handling
        }
        finally
        {
            // Ensure the unmanaged memory is zeroed and freed
            Log.Debug("Zeroing and freeing unmanaged memory for SecureString");
            if (ptr != IntPtr.Zero)
            {
                // zero & free
                for (var i = 0; i < secureString.Length; i++)
                {
                    Marshal.WriteInt16(ptr, i * 2, 0);
                }

                Marshal.ZeroFreeCoTaskMemUnicode(ptr);
            }
        }
    }
    /// <summary>
    /// Converts a <see cref="ReadOnlySpan{Char}"/> to a <see cref="SecureString"/>.
    /// </summary>
    /// <param name="span">The character span to convert.</param>
    /// <returns>A read-only <see cref="SecureString"/> containing the characters from the span.</returns>
    /// <exception cref="ArgumentException">Thrown if the span is empty.</exception>
    public static SecureString ToSecureString(this ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
        {
            throw new ArgumentException("Span is empty", nameof(span));
        }

        var secure = new SecureString();
        foreach (var c in span)
        {
            secure.AppendChar(c);
        }

        secure.MakeReadOnly();
        return secure;
    }
}
