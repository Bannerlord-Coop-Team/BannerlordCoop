using Common.Messaging;
using GameInterface.Services.Heroes.Data;

namespace GameInterface.Services.Heroes.Messages.Lifetime;
public record CreateHero : ICommand
{
    public CreateHero(HeroCreationData heroCreationData)
    {
        Data = heroCreationData;
    }

    public HeroCreationData Data { get; }
}
