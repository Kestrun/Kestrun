using System.Globalization;
using System.Text;
using System.Text.Json;

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
            var trimmed = line.Trim();

            if (ShouldSkipLine(trimmed))
            {
                continue;
            }

            if (string.Equals(trimmed, "}", StringComparison.Ordinal))
            {
                ProcessClosingBrace(prefixStack);
                continue;
            }

            ProcessKeyValueLine(trimmed, prefixStack, map);
        }

        return map;
    }

    /// <summary>
    /// Parses a JSON string table file and returns a dictionary of key/value pairs.
    /// </summary>
    /// <param name="path">The JSON file path to parse.</param>
    /// <returns>A dictionary containing parsed key/value pairs.</returns>
    public static IReadOnlyDictionary<string, string> ParseJsonFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Localization file not found.", path);
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            FlattenJsonElement(document.RootElement, prefix: null, map);
        }

        return map;
    }

    /// <summary>
    /// Determines whether a line should be skipped during parsing.
    /// </summary>
    /// <param name="trimmed">The trimmed line content.</param>
    /// <returns>True if the line should be skipped; otherwise false.</returns>
    private static bool ShouldSkipLine(string trimmed) =>
        trimmed.Length == 0 ||
        string.Equals(trimmed, "@{", StringComparison.Ordinal) ||
        trimmed.StartsWith('#') ||
        trimmed.StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Processes a closing brace by popping the prefix stack.
    /// </summary>
    /// <param name="prefixStack">The prefix stack tracking nested hashtables.</param>
    private static void ProcessClosingBrace(Stack<string> prefixStack)
    {
        if (prefixStack.Count > 0)
        {
            _ = prefixStack.Pop();
        }
    }

    /// <summary>
    /// Processes a key=value line and adds it to the map.
    /// </summary>
    /// <param name="trimmed">The trimmed line content.</param>
    /// <param name="prefixStack">The prefix stack tracking nested hashtables.</param>
    /// <param name="map">The dictionary to populate.</param>
    private static void ProcessKeyValueLine(string trimmed, Stack<string> prefixStack, Dictionary<string, string> map)
    {
        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex < 0)
        {
            return;
        }

        var key = trimmed[..equalsIndex].Trim();
        if (key.Length == 0)
        {
            return;
        }

        var value = trimmed[(equalsIndex + 1)..].Trim();
        if (string.Equals(value, "@{", StringComparison.Ordinal))
        {
            var prefix = prefixStack.Count == 0 ? key : string.Concat(prefixStack.Peek(), ".", key);
            prefixStack.Push(prefix);
            return;
        }

        value = UnquoteValue(value);
        var fullKey = prefixStack.Count == 0 ? key : string.Concat(prefixStack.Peek(), ".", key);
        map[fullKey] = value;
    }

    /// <summary>
    /// Flattens a JSON element into dot-delimited keys.
    /// </summary>
    /// <param name="element">The JSON element to flatten.</param>
    /// <param name="prefix">The key prefix.</param>
    /// <param name="map">The dictionary to populate.</param>
    private static void FlattenJsonElement(JsonElement element, string? prefix, Dictionary<string, string> map)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var nextPrefix = BuildJsonKey(prefix, property.Name);
                FlattenJsonElement(property.Value, nextPrefix, map);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var nextPrefix = BuildJsonKey(prefix, index.ToString(CultureInfo.InvariantCulture));
                FlattenJsonElement(item, nextPrefix, map);
                index++;
            }

            return;
        }

        if (prefix is not null)
        {
            map[prefix] = ConvertJsonValue(element);
        }
    }

    /// <summary>
    /// Builds a dot-delimited JSON key.
    /// </summary>
    /// <param name="prefix">The existing prefix.</param>
    /// <param name="name">The next segment name.</param>
    /// <returns>The combined key.</returns>
    private static string BuildJsonKey(string? prefix, string name) =>
        string.IsNullOrWhiteSpace(prefix) ? name : string.Concat(prefix, ".", name);

    /// <summary>
    /// Converts a JSON element to a string value for the string table.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <returns>The string value.</returns>
    private static string ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Removes surrounding quotes from a value if present.
    /// </summary>
    /// <param name="value">The value to unquote.</param>
    /// <returns>The unquoted value.</returns>
    private static string UnquoteValue(string value)
    {
        if (value.Length >= 2)
        {
            var quote = value[0];
            if ((quote == '"' || quote == '\'') && value[^1] == quote)
            {
                return value[1..^1];
            }
        }

        return value;
    }
}
