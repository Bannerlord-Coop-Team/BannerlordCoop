using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player applied the ordered campaign join tail and can receive normal world state.
/// </summary>
internal record PlayerCampaignSynchronized : IEvent
{
    public NetPeer PlayerId { get; }

    public PlayerCampaignSynchronized(NetPeer playerId)
    {
        PlayerId = playerId;
    }
}
