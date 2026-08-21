using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Towns.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDailyTickDrinkThisDayInSettlement : ICommand {}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkWeeklyTickHasBoughtTunToParty : ICommand {}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerAcceptsClanInfoOffer : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkPlayerAcceptsClanInfoOffer(string mainHeroId)
    {
        MainHeroId = mainHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTavernMaidDeliversFood : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string CurrentSettlementId;

    public NetworkTavernMaidDeliversFood(
        string mainHeroId,
        string currentSettlementId)
    {
        MainHeroId = mainHeroId;
        CurrentSettlementId = currentSettlementId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBuysTun : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly int TunPrice;

    public NetworkPlayerBuysTun(
        string mainHeroId,
        int tunPrice)
    {
        MainHeroId = mainHeroId;
        TunPrice = tunPrice;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateHasMetRansomBroker : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly bool HasMetRansomBroker;

    public NetworkUpdateHasMetRansomBroker(
        string mainHeroId,
        bool hasMetRansomBroker)
    {
        MainHeroId = mainHeroId;
        HasMetRansomBroker = hasMetRansomBroker;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTavernKeeperFindCompanion : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkTavernKeeperFindCompanion(string mainHeroId)
    {
        MainHeroId = mainHeroId;
    }
}