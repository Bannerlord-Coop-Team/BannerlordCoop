using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

public readonly struct HeadmanGrainPriceCached : IEvent
{
    public readonly int Price;

    public HeadmanGrainPriceCached(int price)
    {
        Price = price;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanGrainPriceUpdated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly int Price;

    public NetworkHeadmanGrainPriceUpdated(int price)
    {
        Price = price;
    }
}
