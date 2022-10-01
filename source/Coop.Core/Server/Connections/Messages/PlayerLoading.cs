using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerLoading : ICommand
    {
        public PlayerLoading(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
