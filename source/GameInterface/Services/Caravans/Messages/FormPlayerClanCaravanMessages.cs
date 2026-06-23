using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Caravans.Messages;

public readonly struct FormPlayerClanCaravan : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero CaravanLeader;
    public readonly Settlement CurrentSettlement;
    public readonly bool IsElite;
    public readonly bool ShouldCreateConvoy;
    public readonly int GoldCost;

    public FormPlayerClanCaravan(
        Hero mainHero,
        Hero caravanLeader,
        Settlement currentSettlement,
        bool isElite,
        bool shouldCreateConvoy,
        int goldCost)
    {
        MainHero = mainHero;
        CaravanLeader = caravanLeader;
        CurrentSettlement = currentSettlement;
        IsElite = isElite;
        ShouldCreateConvoy = shouldCreateConvoy;
        GoldCost = goldCost;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkFormPlayerClanCaravan : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string CaravanLeaderId;

    [ProtoMember(3)]
    public readonly string CurrentSettlementId;

    [ProtoMember(4)]
    public readonly bool IsElite;

    [ProtoMember(5)]
    public readonly bool ShouldCreateConvoy;

    [ProtoMember(6)]
    public readonly int GoldCost;

    public NetworkFormPlayerClanCaravan(
        string mainHeroId,
        string caravanLeaderId,
        string currentSettlementId,
        bool isElite,
        bool shouldCreateConvoy,
        int goldCost)
    {
        MainHeroId = mainHeroId;
        CaravanLeaderId = caravanLeaderId;
        CurrentSettlementId = currentSettlementId;
        IsElite = isElite;
        ShouldCreateConvoy = shouldCreateConvoy;
        GoldCost = goldCost;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkFadeOutNewCaravanLeader : ICommand
{
    [ProtoMember(1)]
    public readonly string CaravanLeaderId;

    public NetworkFadeOutNewCaravanLeader(string caravanLeaderId)
    {
        CaravanLeaderId = caravanLeaderId;
    }
}