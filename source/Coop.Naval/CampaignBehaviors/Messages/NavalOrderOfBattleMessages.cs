using Common.Messaging;
using GameInterface.Services.OrderOfBattle.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static NavalDLC.CampaignBehaviors.NavalOrderOfBattleCampaignBehavior;

namespace Coop.Naval.CampaignBehaviors.Messages;

public readonly struct NavalOrderOfBattleSaved : IEvent
{
    public readonly Hero MainHero;
    public readonly List<NavalOrderOfBattleFormationData> FormationInfos;
    public readonly bool IsInArmy;

    public NavalOrderOfBattleSaved(
        Hero mainHero,
        List<NavalOrderOfBattleFormationData> formationInfos,
        bool isInArmy)
    {
        MainHero = mainHero;
        FormationInfos = formationInfos;
        IsInArmy = isInArmy;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSaveNavalOrderOfBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly NavalOrderOfBattleFormation[] Formations;

    [ProtoMember(3)]
    public readonly bool IsInArmy;

    public NetworkSaveNavalOrderOfBattle(
        string mainHeroId,
        NavalOrderOfBattleFormation[] formations,
        bool isInArmy)
    {
        MainHeroId = mainHeroId;
        Formations = formations;
        IsInArmy = isInArmy;
    }
}
