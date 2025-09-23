using System.Collections;
using System.Management.Automation;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// YAML type converter for PSObject types
/// </summary>
/// <param name="omitNullValues">If true, null values will be omitted from the output</param>
/// <param name="useFlowStyle">If true, the mapping will be emitted in flow style</param>
public class PSObjectTypeConverter(bool omitNullValues = false, bool useFlowStyle = false) : IYamlTypeConverter
{
    private readonly bool omitNullValues = omitNullValues;
    private readonly bool useFlowStyle = useFlowStyle;

    /// <summary>
    /// Check if the type is PSObject
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>true if the type is PSObject; otherwise, false.</returns>
    public bool Accepts(Type type) => typeof(PSObject).IsAssignableFrom(type);

    /// <summary>
    /// Read a PSObject from YAML
    /// </summary>
    /// <param name="parser">The YAML parser</param>
    /// <param name="type">The target type</param>
    /// <param name="rootDeserializer">The root deserializer</param>
    /// <returns>The deserialized PSObject</returns>
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        // We don't really need to do any custom deserialization.
        var deserialized = rootDeserializer(typeof(IDictionary<string, object>)) as IDictionary;
        // Wrap the result in a PSObject so we never return null; if deserialized is null, the PSObject's BaseObject will be null.
        return new PSObject(deserialized);
    }

    /// <summary>
    /// Write a PSObject to YAML
    /// </summary>
    /// <param name="emitter">The YAML emitter</param>
    /// <param name="value">The PSObject to serialize</param>
    /// <param name="type">The target type</param>
    /// <param name="serializer">The object serializer</param>
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value == null)
        {
            // Emit YAML null via the serializer
            serializer(null, typeof(object));
            return;
        }

        var psObj = (PSObject)value;
        if (psObj.BaseObject == null || (!typeof(IDictionary).IsAssignableFrom(psObj.BaseObject.GetType()) && !typeof(PSCustomObject).IsAssignableFrom(psObj.BaseObject.GetType())))
        {
            serializer(psObj.BaseObject, psObj.BaseObject?.GetType() ?? typeof(object));
            return;
        }
        var mappingStyle = useFlowStyle ? MappingStyle.Flow : MappingStyle.Block;
        emitter.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, true, mappingStyle));
        foreach (var prop in psObj.Properties)
        {
            if (prop.Value == null)
            {
                if (omitNullValues)
                {
                    continue;
                }
                // For PSCustomObject tests expect literal 'null' not blank scalar
                serializer(prop.Name, prop.Name.GetType());
                emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, "null", ScalarStyle.Plain, true, false));
                continue;
            }

            serializer(prop.Name, prop.Name.GetType());
            var objType = prop.Value.GetType();
            var val = prop.Value;
            if (val is string s2 && s2.Length == 0)
            {
                // Explicitly emit double-quoted empty string to distinguish from blank null
                emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, string.Empty, ScalarStyle.DoubleQuoted, true, false));
                continue;
            }
            // Let the default serializer handle IList types to preserve flow / block style decisions.
            if (prop.Value is PSObject nestedPsObj)
            {
                var nestedType = nestedPsObj.BaseObject?.GetType();
                if (nestedType != null && nestedType != typeof(PSCustomObject))
                {
                    objType = nestedType!;
                    val = nestedPsObj.BaseObject!;
                }
            }
            serializer(val, objType);
        }
        emitter.Emit(new MappingEnd());
    }
}
