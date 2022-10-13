using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct ResolvedCharacter : ICommand
    {
        public ResolvedCharacter(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
