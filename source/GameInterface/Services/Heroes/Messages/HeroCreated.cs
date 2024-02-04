using Common.Messaging;
using GameInterface.Services.Heroes.Data;

namespace GameInterface.Services.Heroes.Messages;

public record HeroCreated : IEvent
{
    public HeroCreationData Data { get; }

    public HeroCreated(HeroCreationData data)
    {
        Data = data;
    }
}