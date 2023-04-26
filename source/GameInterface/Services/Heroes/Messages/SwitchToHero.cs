using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct SwitchToHero
    {
        public string HeroId { get; }

        public SwitchToHero(string heroId)
        {
            HeroId = heroId;
        }
    }
}
