using System.Text.RegularExpressions;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Provides helper methods for serializing and deserializing YAML content, with special handling for PowerShell objects.
/// </summary>
public static partial class YamlHelper
{
    /// <summary>
    /// Serializes any PowerShell object to YAML format, with specified serialization options.
    /// </summary>
    /// <param name="input">The PowerShell object to serialize. Can be null.</param>
    /// <param name="options">The serialization options to apply.</param>
    /// <returns>A string containing the YAML representation of the input object.</returns>
    public static string ToYaml(object? input, SerializationOptions? options = null)
    {
        var wrt = new StringWriter();
        // Default options intentionally omit Roundtrip to allow serialization of anonymous types
        // without requiring default constructors (Roundtrip enforces reconstructable object graphs).
        options ??= SerializationOptions.DisableAliases | SerializationOptions.EmitDefaults | SerializationOptions.WithIndentedSequences;
        var serializer = YamlSerializerFactory.GetSerializer(options.Value);

        serializer.Serialize(wrt, input);
        // Post-process: convert null dictionary entries serialized as '' into blank null form (key: \n)
        // Safe regex: only targets single-quoted empty string immediately after colon with optional space.
        return MyRegex().Replace(wrt.ToString(), "${k}:");
    }

    [GeneratedRegex(@"^(?<k>[^:\r\n]+):\s*''\s*$", RegexOptions.Multiline)]
    private static partial Regex MyRegex();
}
