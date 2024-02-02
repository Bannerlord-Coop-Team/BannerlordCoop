using Common.Messaging;
using GameInterface.Services.Heroes.Data;

namespace GameInterface.Services.Heroes.Messages;
public record CreateHero : ICommand
{
    public CreateHero(HeroCreationData heroCreationData)
    {
        HeroCreationData = heroCreationData;
    }

    public HeroCreationData HeroCreationData { get; }
}
