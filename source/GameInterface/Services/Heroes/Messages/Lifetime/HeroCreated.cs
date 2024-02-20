using Common.Messaging;
using GameInterface.Services.Heroes.Data;

namespace GameInterface.Services.Heroes.Messages.Lifetime;

/// <summary>
/// Event for when a hero is created.
/// </summary>
public record HeroCreated : IEvent
{
    public HeroCreationData Data { get; }

    public HeroCreated(HeroCreationData data)
    {
        Data = data;
    }
}