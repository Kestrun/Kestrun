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
        // Use PSObject.AsPSObject to avoid wrapping an already wrapped PSObject and to safely handle null values.
        return PSObject.AsPSObject(deserialized ?? new Hashtable());
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
        if (value is null)
        {
            EmitNull(serializer);
            return;
        }

        var psObj = (PSObject)value;
        if (!IsDictionaryLike(psObj))
        {
            SerializeNonDictionary(psObj, serializer);
            return;
        }

        BeginMapping(emitter);
        foreach (var prop in psObj.Properties)
        {
            WriteProperty(emitter, prop, serializer);
        }
        EndMapping(emitter);
    }

    // ----------------- Helper Methods -----------------
    private static void EmitNull(ObjectSerializer serializer) => serializer(null, typeof(object));

    private static bool IsDictionaryLike(PSObject psObj)
    {
        var baseObj = psObj.BaseObject;
        if (baseObj is null)
        {
            return false;
        }

        return baseObj is IDictionary ? true : baseObj is PSCustomObject;
    }

    private static void SerializeNonDictionary(PSObject psObj, ObjectSerializer serializer)
        => serializer(psObj.BaseObject, psObj.BaseObject?.GetType() ?? typeof(object));

    private void BeginMapping(IEmitter emitter)
    {
        var mappingStyle = useFlowStyle ? MappingStyle.Flow : MappingStyle.Block;
        emitter.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, true, mappingStyle));
    }

    private static void EndMapping(IEmitter emitter) => emitter.Emit(new MappingEnd());

    private void WriteProperty(IEmitter emitter, PSPropertyInfo prop, ObjectSerializer serializer)
    {
        if (prop.Value is null)
        {
            if (omitNullValues)
            {
                return; // skip entirely
            }
            EmitNullProperty(emitter, prop, serializer);
            return;
        }

        // Emit key
        serializer(prop.Name, prop.Name.GetType());

        var (val, type) = UnwrapValue(prop.Value);

        if (val is string s && s.Length == 0)
        {
            // Double-quoted explicit empty string
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, string.Empty, ScalarStyle.DoubleQuoted, true, false));
            return;
        }

        serializer(val, type);
    }

    private void EmitNullProperty(IEmitter emitter, PSPropertyInfo prop, ObjectSerializer serializer)
    {
        serializer(prop.Name, prop.Name.GetType());
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, "null", ScalarStyle.Plain, true, false));
    }

    private static (object value, Type type) UnwrapValue(object raw)
    {
        if (raw is PSObject nested)
        {
            var nestedType = nested.BaseObject?.GetType();
            if (nestedType != null && nestedType != typeof(PSCustomObject) && nested.BaseObject != null)
            {
                return (nested.BaseObject, nestedType);
            }
        }
        return (raw, raw.GetType());
    }
}
