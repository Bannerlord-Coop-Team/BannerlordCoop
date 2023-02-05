using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct ResolveHeroNotFound : IEvent
    {
        public int PeerId { get; }

        public ResolveHeroNotFound(int peerId)
        {
            PeerId = peerId;
        }
    }
}