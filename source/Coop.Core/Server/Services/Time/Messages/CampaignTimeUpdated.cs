using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// Authoritative campaign time broadcast from the server to all clients.
/// </summary>
/// <remarks>
/// Sent once per second by the server. Clients smoothly interpolate their
/// local <c>MapTimeTracker</c> tick value toward <see cref="ServerTicks"/>.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public sealed class CampaignTimeUpdated : IEvent
{
    [ProtoMember(1)]
    public long ServerTicks { get; }

    public CampaignTimeUpdated(long serverTicks)
    {
        ServerTicks = serverTicks;
    }
}
