using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Utility class for loading and parsing YAML documents
/// </summary>
public static class YamlLoader
{
    /// <summary>
    /// Parses one or more YAML documents from a string and returns a YamlStream.
    /// Set <paramref name="useMergingParser"/> to true to enable YAML anchors/aliases mergehandling.
    /// </summary>
    /// <param name="yaml">The YAML string to parse.</param>
    /// <param name="useMergingParser">Whether to use a merging parser to handle anchors and aliases.</param>
    /// <returns>A YamlStream containing the parsed documents.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the input YAML string is null.</exception>
    public static YamlStream GetYamlDocuments(string yaml, bool useMergingParser = false)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        using var reader = new StringReader(yaml);

        IParser parser = new Parser(reader);          // YamlDotNet.Core.Parser
        if (useMergingParser)
        {
            // YamlDotNet.Core.MergingParser wraps an existing parser
            parser = new MergingParser(parser);
        }

        var stream = new YamlStream();                // YamlDotNet.RepresentationModel.YamlStream
        stream.Load(parser);                          // parse the stream (may contain multiple docs)
        return stream;
    }

    /// <summary>
    /// Convenience: returns each document's root node from a yaml string.
    /// </summary>
    public static IReadOnlyList<YamlNode> GetRootNodes(string yaml, bool useMergingParser = false)
    {
        var ys = GetYamlDocuments(yaml, useMergingParser);
        return [.. ys.Documents.Select(d => d.RootNode)];
    }

    /// <summary>
    /// Convenience: fully convert to .NET objects using your converter (mapping→dict, seq→array, scalar→typed).
    /// </summary>
    public static IReadOnlyList<object?> DeserializeToObjects(string yaml, bool useMergingParser = false)
    {
        var ys = GetYamlDocuments(yaml, useMergingParser);
        var result = new List<object?>(ys.Documents.Count);
        foreach (var doc in ys.Documents)
        {
            result.Add(YamlTypeConverter.ConvertYamlDocumentToPSObject(doc.RootNode, ordered: false));
        }
        return result;
    }
}
