using System.Text;

namespace Kestrun.Localization;

/// <summary>
/// Parses PowerShell-style string table files containing key=value pairs.
/// </summary>
public static class StringTableParser
{
    /// <summary>
    /// Parses a string table file and returns a dictionary of key/value pairs.
    /// </summary>
    /// <param name="path">The file path to parse.</param>
    /// <returns>A dictionary containing parsed key/value pairs.</returns>
    public static IReadOnlyDictionary<string, string> ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Localization file not found.", path);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var prefixStack = new Stack<string>();
        using var reader = new StreamReader(
            path,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (string.Equals(trimmed, "@{", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(trimmed, "}", StringComparison.Ordinal))
            {
                if (prefixStack.Count > 0)
                {
                    _ = prefixStack.Pop();
                }
                continue;
            }

            if (trimmed.StartsWith('#') ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var key = trimmed[..equalsIndex].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            var value = trimmed[(equalsIndex + 1)..].Trim();
            if (string.Equals(value, "@{", StringComparison.Ordinal))
            {
                var prefix = prefixStack.Count == 0 ? key : string.Concat(prefixStack.Peek(), ".", key);
                prefixStack.Push(prefix);
                continue;
            }

            if (value.Length >= 2)
            {
                var quote = value[0];
                if ((quote == '"' || quote == '\'') && value[^1] == quote)
                {
                    value = value[1..^1];
                }
            }

            var fullKey = prefixStack.Count == 0 ? key : string.Concat(prefixStack.Peek(), ".", key);
            map[fullKey] = value;
        }

        return map;
    }
}
