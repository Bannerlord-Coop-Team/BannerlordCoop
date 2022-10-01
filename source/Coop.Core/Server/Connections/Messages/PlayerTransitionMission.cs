using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerTransitionMission : ICommand
    {
        public PlayerTransitionMission(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
