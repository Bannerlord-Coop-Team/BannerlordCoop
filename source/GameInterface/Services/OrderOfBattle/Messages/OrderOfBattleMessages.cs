using Common.Messaging;
using GameInterface.Services.OrderOfBattle.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.OrderOfBattleCampaignBehavior;

namespace GameInterface.Services.OrderOfBattle.Messages;

public readonly struct InitializeClientOrderOfBattleData : IEvent
{
    public readonly OrderOfBattlePlayerData OrderOfBattlePlayerData;

    public InitializeClientOrderOfBattleData(OrderOfBattlePlayerData orderOfBattlePlayerData)
    {
        OrderOfBattlePlayerData = orderOfBattlePlayerData;
    }
}

public readonly struct OrderOfBattleSaved : IEvent
{
    public readonly Hero MainHero;
    public readonly List<OrderOfBattleFormationData> FormationInfos;
    public readonly bool IsSiegeBattle;
    public readonly bool IsInArmy;

    public OrderOfBattleSaved(
        Hero mainHero,
        List<OrderOfBattleFormationData> formationInfos,
        bool isSiegeBattle,
        bool isInArmy)
    {
        MainHero = mainHero;
        FormationInfos = formationInfos;
        IsSiegeBattle = isSiegeBattle;
        IsInArmy = isInArmy;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSaveOrderOfBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly OrderOfBattleFormation[] Formations;

    [ProtoMember(3)]
    public readonly bool IsSiegeBattle;

    [ProtoMember(4)]
    public readonly bool IsInArmy;

    public NetworkSaveOrderOfBattle(
        string mainHeroId,
        OrderOfBattleFormation[] formations,
        bool isSiegeBattle,
        bool isInArmy)
    {
        MainHeroId = mainHeroId;
        Formations = formations;
        IsSiegeBattle = isSiegeBattle;
        IsInArmy = isInArmy;
    }
}
