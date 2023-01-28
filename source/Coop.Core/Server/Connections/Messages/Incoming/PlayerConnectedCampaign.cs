using Common.Messaging;
using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.Messages.Incoming
{
    public readonly struct PlayerConnectedCampaign : ICommand
    {
        public PlayerConnectedCampaign(NetPeer playerId)
        {
            PlayerId = playerId;
        }

        public NetPeer PlayerId { get; }
    }
}
