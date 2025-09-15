using System.Text.RegularExpressions;

namespace Kestrun.Utilities;

internal static class RegexUtils
{
    /// <summary>
    /// Checks if the input string matches the glob pattern.
    /// The pattern can contain '*' for any sequence of characters and '?' for a single character.
    /// </summary>
    /// <param name="input">The input string to match against the pattern.</param>
    /// <param name="pattern">The glob pattern to match.</param>
    /// <param name="ignoreCase">Whether to ignore case when matching.</param>
    /// <returns>True if the input matches the pattern, otherwise false.</returns>
    public static bool IsGlobMatch(string input, string pattern, bool ignoreCase = true)
    {
        // Escape regex metacharacters then bring back * and ?
        var re = "^" + Regex.Escape(pattern)
                            .Replace(@"\*", ".*")
                            .Replace(@"\?", ".") + "$";

        var flags = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return Regex.IsMatch(input, re, flags);
    }
}
