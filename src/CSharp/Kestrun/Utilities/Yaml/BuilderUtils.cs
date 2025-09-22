using YamlDotNet.Serialization;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Utility class for building YAML serializers with common settings
/// </summary>
internal class BuilderUtils
{
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
