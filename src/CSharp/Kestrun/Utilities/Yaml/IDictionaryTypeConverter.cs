using System.Collections;
using System.Management.Automation;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// YAML type converter for IDictionary types
/// </summary>
/// <remarks>
/// Constructor
/// </remarks>
/// <param name="omitNullValues">If true, null values will be omitted from the output</param>
/// <param name="useFlowStyle">If true, the mapping will be emitted in flow style</param>
public class IDictionaryTypeConverter(bool omitNullValues = false, bool useFlowStyle = false) : IYamlTypeConverter
{

    private readonly bool omitNullValues = omitNullValues;
    private readonly bool useFlowStyle = useFlowStyle;

    /// <summary>
    /// Check if the type is IDictionary
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>true if the type is IDictionary; otherwise, false.</returns>
    public bool Accepts(Type type)
    {
        return typeof(IDictionary).IsAssignableFrom(type);
    }

    /// <summary>
    /// Read an IDictionary from YAML
    /// </summary>
    /// <param name="parser">The YAML parser</param>
    /// <param name="type">The type of the object to deserialize</param>
    /// <param name="rootDeserializer">The root deserializer</param>
    /// <returns>The deserialized object</returns>
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var deserializedObject = rootDeserializer(typeof(IDictionary<string, object>)) as IDictionary;
        // Ensure a non-null IDictionary is returned to satisfy nullable reference expectations.
        return deserializedObject ?? new Hashtable();
    }

    /// <summary>
    /// Write an IDictionary to YAML
    /// </summary>
    /// <param name="emitter">The YAML emitter</param>
    /// <param name="value">The IDictionary to serialize</param>
    /// <param name="type">The type of the object to serialize</param>
    /// <param name="serializer">The object serializer</param>
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value == null)
        {
            // Emit explicit YAML null for a null dictionary value
            emitter.Emit(new Scalar(AnchorName.Empty, "tag:yaml.org,2002:null", "", ScalarStyle.Plain, true, false));
            return;
        }

        var hObj = (IDictionary)value;
        var mappingStyle = useFlowStyle ? MappingStyle.Flow : MappingStyle.Block;

        emitter.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, true, mappingStyle));
        foreach (DictionaryEntry entry in hObj)
        {
            if (entry.Value == null)
            {
                if (omitNullValues)
                {
                    continue;
                }
                // Emit a blank plain scalar (no quotes) to represent YAML null for dictionary entries (implicit null, no tag).
                serializer(entry.Key, entry.Key.GetType());
                // Must set at least one implicit flag to true when tag is empty; plain implicit=true allows empty scalar as implicit null.
                emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, string.Empty, ScalarStyle.Plain, true, false));
                continue;
            }
            serializer(entry.Key, entry.Key.GetType());
            var objType = entry.Value.GetType();
            var val = entry.Value;
            // If empty string, emit explicitly as double-quoted to satisfy tests distinguishing from null blank.
            if (val is string s && s.Length == 0)
            {
                emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, string.Empty, ScalarStyle.DoubleQuoted, true, false));
                continue;
            }
            if (entry.Value is PSObject nestedObj)
            {
                var nestedType = nestedObj.BaseObject.GetType();
                if (nestedType != typeof(PSCustomObject))
                {
                    objType = nestedObj.BaseObject.GetType();
                    val = nestedObj.BaseObject;
                }
                serializer(val, objType);
            }
            else
            {
                serializer(entry.Value, entry.Value.GetType());
            }
        }
        emitter.Emit(new MappingEnd());
    }
}
