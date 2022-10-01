using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerDisconnected : ICommand
    {
        public PlayerDisconnected(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
