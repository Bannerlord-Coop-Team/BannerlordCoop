using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct ResolveCharacter : ICommand
    {
        public ResolveCharacter(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }
}
