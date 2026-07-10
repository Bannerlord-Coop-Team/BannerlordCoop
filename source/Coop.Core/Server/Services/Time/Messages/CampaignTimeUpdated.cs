using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// Authoritative campaign time broadcast from the server to all clients.
/// </summary>
/// <remarks>
/// Sent four times per second by the server. Clients use it to keep their
/// campaign simulation paced with the authoritative server.
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
