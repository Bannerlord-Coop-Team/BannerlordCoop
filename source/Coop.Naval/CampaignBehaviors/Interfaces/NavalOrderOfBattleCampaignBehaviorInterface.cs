using GameInterface.CoopSessionData;
using GameInterface.Services;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.OrderOfBattle;
using GameInterface.Services.OrderOfBattle.Data;
using NavalDLC.CampaignBehaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using static NavalDLC.CampaignBehaviors.NavalOrderOfBattleCampaignBehavior;

namespace Coop.Naval.CampaignBehaviors.Interfaces;

public interface INavalOrderOfBattleCampaignBehaviorInterface : IGameAbstraction
{
    NavalOrderOfBattleFormation[] CreateFormations(List<NavalOrderOfBattleFormationData> formationInfos);
    void LoadFormations(OrderOfBattleData orderOfBattle);
    void SetPlayerFormations(string playerHeroId, NavalOrderOfBattleFormation[] formations, bool isInArmy);
}

public class NavalOrderOfBattleCampaignBehaviorInterface : INavalOrderOfBattleCampaignBehaviorInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;

    private OrderOfBattlePlayerData OrderOfBattlePlayerData => coopSessionProvider.CoopSession.OrderOfBattlePlayerData;

    public NavalOrderOfBattleCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
    }

    public NavalOrderOfBattleFormation[] CreateFormations(List<NavalOrderOfBattleFormationData> formationInfos)
    {
        return formationInfos.Select(formationInfo =>
        {
            objectManager.TryGetId(formationInfo.Captain, out var captainId);
            objectManager.TryGetId(formationInfo.Ship, out var shipId);

            return new NavalOrderOfBattleFormation(
                captainId,
                shipId,
                formationInfo.FormationClass,
                formationInfo.Filters);
        }).ToArray();
    }

    public void LoadFormations(OrderOfBattleData orderOfBattle)
    {
        var behavior = Campaign.Current.GetCampaignBehavior<NavalOrderOfBattleCampaignBehavior>();

        // Replaces whatever the server save held, which isn't this player's order of battle
        behavior._navalBattleFormationInfos = CreateFormationInfos(orderOfBattle?.NavalBattleFormations);
        behavior._navalBattleArmyFormationInfos = CreateFormationInfos(orderOfBattle?.NavalBattleArmyFormations);
    }

    public void SetPlayerFormations(string playerHeroId, NavalOrderOfBattleFormation[] formations, bool isInArmy)
    {
        if (!OrderOfBattlePlayerData.PlayerOrderOfBattles.TryGetValue(playerHeroId, out var orderOfBattle))
        {
            orderOfBattle = new OrderOfBattleData();
            OrderOfBattlePlayerData.PlayerOrderOfBattles[playerHeroId] = orderOfBattle;
        }

        if (isInArmy) orderOfBattle.NavalBattleArmyFormations = formations;
        else orderOfBattle.NavalBattleFormations = formations;
    }

    private List<NavalOrderOfBattleFormationData> CreateFormationInfos(NavalOrderOfBattleFormation[] formations)
    {
        // A captain or ship that no longer resolves is left empty, like vanilla does once they are gone
        return (formations ?? Array.Empty<NavalOrderOfBattleFormation>()).Select(formation =>
        {
            objectManager.TryGetObject<Hero>(formation.CaptainId, out var captain);
            objectManager.TryGetObject<Ship>(formation.ShipId, out var ship);

            return new NavalOrderOfBattleFormationData(
                captain,
                ship,
                formation.FormationClass,
                formation.Filters ?? new());
        }).ToList();
    }
}
