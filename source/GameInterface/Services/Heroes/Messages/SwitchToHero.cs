using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;

public record SwitchToHero : ICommand
{
    public string HeroId { get; }

    public SwitchToHero(string heroId)
    {
        HeroId = heroId;
    }
}
