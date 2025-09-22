using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// YAML object graph visitor that omits null values
/// </summary>
public sealed class NullValueGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor) : ChainedObjectGraphVisitor(nextVisitor)
{
    /// <summary>
    /// YAML object graph visitor that omits null values in mappings
    /// </summary>
    /// <param name="key">The key descriptor</param>
    /// <param name="value">The value descriptor</param>
    /// <param name="context">The emitter context</param>
    /// <param name="serializer">The object serializer</param>
    /// <returns></returns>
    public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context, ObjectSerializer serializer)
    {
        return value.Value != null && base.EnterMapping(key, value, context, serializer);
    }

    /// <summary>
    /// YAML object graph visitor that omits null values in mappings
    /// </summary>
    /// <param name="key">The key descriptor</param>
    /// <param name="value">The value descriptor</param>
    /// <param name="context">The emitter context</param>
    /// <param name="serializer">The object serializer</param>
    /// <returns></returns>
    public override bool EnterMapping(IObjectDescriptor key, IObjectDescriptor value, IEmitter context, ObjectSerializer serializer)
    {
        return value.Value != null && base.EnterMapping(key, value, context, serializer);
    }
}
