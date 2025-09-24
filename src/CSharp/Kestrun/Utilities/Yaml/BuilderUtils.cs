using YamlDotNet.Serialization;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Utility class for building YAML serializers with common settings
/// </summary>
public class BuilderUtils
{
    /// <summary>
    /// Builds a YamlDotNet ISerializerBuilder with common settings.
    /// </summary>
    /// <param name="builder">The serializer builder to configure.</param>
    /// <param name="omitNullValues">Whether to omit null values.</param>
    /// <param name="useFlowStyle">Whether to use flow style for collections.</param>
    /// <param name="useSequenceFlowStyle">Whether to use flow style for sequences.</param>
    /// <param name="jsonCompatible">Whether to make the output JSON compatible.</param>
    /// <returns>The configured serializer builder.</returns>
    public static SerializerBuilder BuildSerializer(
        SerializerBuilder builder,
        bool omitNullValues = false,
        bool useFlowStyle = false,
        bool useSequenceFlowStyle = false,
        bool jsonCompatible = false)
    {
        if (jsonCompatible)
        {
            useFlowStyle = true;
            useSequenceFlowStyle = true;
        }

        builder = builder
            .WithEventEmitter(next => new StringQuotingEmitter(next))
            .WithTypeConverter(new BigIntegerTypeConverter())
            .WithTypeConverter(new IDictionaryTypeConverter(omitNullValues, useFlowStyle))
            .WithTypeConverter(new PSObjectTypeConverter(omitNullValues, useFlowStyle));
        // Use platform newline; tests explicitly reference [Environment]::NewLine for single-line cases and replace it for flow/json cases
        //.WithNewLine("\n");

        if (omitNullValues)
        {
            builder = builder
                .WithEmissionPhaseObjectGraphVisitor(args => new NullValueGraphVisitor(args.InnerVisitor));
        }
        if (useFlowStyle)
        {
            builder = builder.WithEventEmitter(next => new FlowStyleAllEmitter(next));
        }
        if (useSequenceFlowStyle)
        {
            builder = builder.WithEventEmitter(next => new FlowStyleSequenceEmitter(next));
        }

        return builder;
    }
}
