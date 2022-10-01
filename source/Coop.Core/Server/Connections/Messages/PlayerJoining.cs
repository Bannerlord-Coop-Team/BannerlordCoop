using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerJoining : ICommand
    {
        public PlayerJoining(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
