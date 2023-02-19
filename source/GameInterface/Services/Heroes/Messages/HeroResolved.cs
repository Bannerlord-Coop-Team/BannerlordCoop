using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct HeroResolved : IEvent
    {
        public int PeerId { get; }
        public string HeroStringId { get; }

        public HeroResolved(int peerId, string heroStringId)
        {
            PeerId = peerId;
            HeroStringId = heroStringId;
        }
    }
}