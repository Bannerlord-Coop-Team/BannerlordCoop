using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player has entered the campaign state
/// </summary>
internal record PlayerCampaignEntered : IEvent
{
    public NetPeer playerId { get; }

    public PlayerCampaignEntered(NetPeer playerId)
    {
        this.playerId = playerId;
    }
}