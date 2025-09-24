using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// YAML emitter that forces sequences to use flow style
/// </summary>
/// <remarks>
/// Constructor
/// </remarks>
/// <param name="next">The next event emitter in the chain</param>
public class FlowStyleSequenceEmitter(IEventEmitter next) : ChainedEventEmitter(next)
{
    /// <inheritdoc/>
    public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
    {
        eventInfo.Style = SequenceStyle.Flow;
        nextEmitter.Emit(eventInfo, emitter);
    }
}
