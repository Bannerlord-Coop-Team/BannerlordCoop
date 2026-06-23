using Common.Messaging;
using GameInterface.Services.Heroes.Data;

namespace GameInterface.Services.Heroes.Messages;
public record HeroNameChanged : IEvent
{
    public HeroNameChanged(HeroChangeNameData data)
    {
        Data = data;
    }

    public HeroChangeNameData Data { get; }
}
