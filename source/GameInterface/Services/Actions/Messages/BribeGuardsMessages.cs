using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Messages;

public readonly struct PlayerBribesGuard : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement Settlement;
    public readonly int Gold;

    public PlayerBribesGuard(
        Hero mainHero,
        Settlement settlement,
        int gold)
    {
        MainHero = mainHero;
        Settlement = settlement;
        Gold = gold;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBribesGuard : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    [ProtoMember(3)]
    public readonly int Gold;

    public NetworkPlayerBribesGuard(
        string mainHeroId,
        string settlementId,
        int gold)
    {
        MainHeroId = mainHeroId;
        SettlementId = settlementId;
        Gold = gold;
    }
}