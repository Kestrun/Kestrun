
using System.Numerics;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// YAML type converter for BigInteger
/// </summary>
public class BigIntegerTypeConverter : IYamlTypeConverter
{
    /// <summary>
    /// Check if the type is BigInteger
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>true if the type is BigInteger; otherwise, false.</returns>
    public bool Accepts(Type type)
    {
        return typeof(BigInteger).IsAssignableFrom(type);
    }

    /// <summary>
    /// Read a BigInteger from YAML
    /// </summary>
    /// <param name="parser">The YAML parser</param>
    /// <param name="type">The type to deserialize to</param>
    /// <param name="rootDeserializer">The root deserializer</param>
    /// <returns>The deserialized BigInteger</returns>
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;
        var bigNr = BigInteger.Parse(value);
        return bigNr;
    }

    /// <summary>
    /// Write a BigInteger to YAML
    /// </summary>
    /// <param name="emitter">The YAML emitter</param>
    /// <param name="value">The BigInteger value</param>
    /// <param name="type">The type of the value</param>
    /// <param name="serializer">The object serializer</param>
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is null)
        {
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, "null", ScalarStyle.Plain, true, false));
            return;
        }

        var bigNr = (BigInteger)value;
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, bigNr.ToString(), ScalarStyle.Plain, true, false));
    }
}
