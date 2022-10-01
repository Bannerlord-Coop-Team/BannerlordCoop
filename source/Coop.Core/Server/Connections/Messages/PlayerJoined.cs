using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerJoined : ICommand
    {
        public PlayerJoined(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
