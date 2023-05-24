using Common.Messaging;

namespace GameInterface.Services.GameDebug.Messages
{
    public record ResolveDebugHero : ICommand
    {
        public string PlayerId { get; }

        public ResolveDebugHero(string playerId)
        {
            PlayerId = playerId;
        }
    }
}
