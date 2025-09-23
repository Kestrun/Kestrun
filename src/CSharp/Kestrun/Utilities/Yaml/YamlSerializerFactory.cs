using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeResolvers;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Factory for creating YamlDotNet serializers with specified options
/// </summary>
public static class YamlSerializerFactory
{
    /// <summary>
    /// Builds a YamlDotNet serializer according to <paramref name="options"/>,
    /// then passes it through BuilderUtils.BuildSerializer(...) to apply
    /// omit-null/flow-style knobs (as in your PowerShell version).
    /// </summary>
    public static ISerializer GetSerializer(SerializationOptions options)
    {
        var builder = new SerializerBuilder();

        var jsonCompatible = options.HasFlag(SerializationOptions.JsonCompatible);

        if (options.HasFlag(SerializationOptions.Roundtrip))
        {
            builder = builder.EnsureRoundtrip();
        }

        if (options.HasFlag(SerializationOptions.DisableAliases))
        {
            builder = builder.DisableAliases();
        }
        if (options.HasFlag(SerializationOptions.EmitDefaults))
        {
            builder = builder.ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve);
        }
        else
        {
            // This matches your old OmitNullValues/EmitDefaults combo
            builder = builder.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);
        }

        if (jsonCompatible)
        {
            builder = builder.JsonCompatible();
        }

        if (options.HasFlag(SerializationOptions.DefaultToStaticType))
        {
            builder = builder.WithTypeResolver(new StaticTypeResolver());
        }

        if (options.HasFlag(SerializationOptions.WithIndentedSequences))
        {
            builder = builder.WithIndentedSequences();
        }

        // Extras handled by your helper (same as PS):
        var omitNullValues = options.HasFlag(SerializationOptions.OmitNullValues);
        var useFlowStyle = options.HasFlag(SerializationOptions.UseFlowStyle);
        var useSequenceFlowStyle = options.HasFlag(SerializationOptions.UseSequenceFlowStyle);

        // Assuming this is a static helper as implied by the call pattern.
        builder = BuilderUtils.BuildSerializer(
            builder,
            omitNullValues,
            useFlowStyle,
            useSequenceFlowStyle,
            jsonCompatible
        );

        return builder.Build();
    }
}
