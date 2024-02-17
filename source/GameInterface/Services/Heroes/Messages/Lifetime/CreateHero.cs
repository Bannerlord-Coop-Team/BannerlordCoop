using Common.Messaging;
using GameInterface.Services.Heroes.Data;

namespace GameInterface.Services.Heroes.Messages.Lifetime;

/// <summary>
/// Command to create a hero.
/// </summary>
public record CreateHero : ICommand
{
    public CreateHero(HeroCreationData heroCreationData)
    {
        Data = heroCreationData;
    }

    public HeroCreationData Data { get; }
}
