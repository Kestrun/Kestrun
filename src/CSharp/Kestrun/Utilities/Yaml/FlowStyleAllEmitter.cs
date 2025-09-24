using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// YAML emitter that forces all mappings and sequences to use flow style
/// </summary>
/// <remarks>
/// Constructor
/// </remarks>
/// <param name="next">The next event emitter in the chain</param>
public class FlowStyleAllEmitter(IEventEmitter next) : ChainedEventEmitter(next)
{
    /// <summary>
    /// Emit a mapping start event with flow style
    /// </summary>
    /// <param name="eventInfo">The mapping start event information</param>
    /// <param name="emitter">The YAML emitter</param>
    public override void Emit(MappingStartEventInfo eventInfo, IEmitter emitter)
    {
        eventInfo.Style = MappingStyle.Flow;
        base.Emit(eventInfo, emitter);
    }

    /// <summary>
    /// Emit a sequence start event with flow style
    /// </summary>
    /// <param name="eventInfo">The sequence start event information</param>
    /// <param name="emitter">The YAML emitter</param>
    public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
    {
        eventInfo.Style = SequenceStyle.Flow;
        nextEmitter.Emit(eventInfo, emitter);
    }
}
