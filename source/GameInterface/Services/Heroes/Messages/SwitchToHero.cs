using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct SwitchToHero
    {
        public Guid HeroId { get; }

        public SwitchToHero(Guid heroId)
        {
            HeroId = heroId;
        }
    }
}
