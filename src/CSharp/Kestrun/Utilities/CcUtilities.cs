namespace Kestrun.Utilities;
/// <summary>
/// Provides utility methods for Kestrun.
/// </summary>
public static class CcUtilities
{
    /// <summary>
    /// Determines whether preview features are enabled in the current AppContext.
    /// </summary>
    public static bool PreviewFeaturesEnabled() =>
        AppContext.TryGetSwitch(
            "System.Runtime.EnablePreviewFeatures", out var on) && on;

    /// <summary>
    /// Returns the line number in the source string at the specified character index.
    /// </summary>
    /// <param name="source">The source string to search.</param>
    /// <param name="index">The character index for which to find the line number.</param>
    /// <returns>The line number at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public static int GetLineNumber(string source, int index)
    {
        if (index < 0 || index > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        // Count how many `\n` occur before the index
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }
        return line;
    }
}
