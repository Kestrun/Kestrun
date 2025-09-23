using Kestrun.Utilities.Yaml;
namespace Kestrun.Utilities;

/// <summary>
/// Provides helper methods for serializing and deserializing YAML content, with special handling for PowerShell objects.
/// </summary>
public static class YamlHelper
{

    /// <summary>
    /// Serializes any PowerShell object to YAML format.
    /// </summary>
    /// <param name="input">The PowerShell object to serialize. Can be null.</param>
    /// <returns>A string containing the YAML representation of the input object.</returns>
    public static string ToYaml(object? input)
    {
        var wrt = new StringWriter();
        var options = SerializationOptions.DisableAliases | SerializationOptions.EmitDefaults | SerializationOptions.WithIndentedSequences;
        var serializer = YamlSerializerFactory.GetSerializer(options);

        serializer.Serialize(wrt, input);
        return wrt.ToString();
    }
     
}
