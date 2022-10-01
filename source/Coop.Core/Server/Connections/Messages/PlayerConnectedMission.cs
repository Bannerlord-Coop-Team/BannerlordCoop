using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct PlayerConnectedMission : ICommand
    {
        public PlayerConnectedMission(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
