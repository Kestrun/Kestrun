using System.Threading.Channels;

namespace Kestrun.Callback;

/// <summary>
/// In-memory queue for callback requests.
/// </summary>
public sealed class InMemoryCallbackQueue
{
    /// <summary>
    /// The channel for callback requests.
    /// </summary>
    public Channel<CallbackRequest> Channel { get; } =
        System.Threading.Channels.Channel.CreateBounded<CallbackRequest>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
}
