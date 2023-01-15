using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerConnectedMission : ICommand
    {
        public PlayerConnectedMission(NetPeer playerId)
        {
            PlayerId = playerId;
        }
        public NetPeer PlayerId { get; }
    }
}
