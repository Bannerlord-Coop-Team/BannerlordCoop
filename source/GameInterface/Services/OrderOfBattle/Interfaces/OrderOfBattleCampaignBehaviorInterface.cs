using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.OrderOfBattle.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.OrderOfBattleCampaignBehavior;

namespace GameInterface.Services.OrderOfBattle.Interfaces;

public interface IOrderOfBattleCampaignBehaviorInterface : IGameAbstraction
{
    OrderOfBattleFormation[] CreateFormations(List<OrderOfBattleFormationData> formationInfos);
    void LoadFormations(OrderOfBattleData orderOfBattle);
    void SetPlayerFormations(string playerHeroId, OrderOfBattleFormation[] formations, bool isSiegeBattle, bool isInArmy);
}

public class OrderOfBattleCampaignBehaviorInterface : IOrderOfBattleCampaignBehaviorInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;

    private OrderOfBattlePlayerData OrderOfBattlePlayerData => coopSessionProvider.CoopSession.OrderOfBattlePlayerData;

    public OrderOfBattleCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
    }

    public OrderOfBattleFormation[] CreateFormations(List<OrderOfBattleFormationData> formationInfos)
    {
        return formationInfos.Select(formationInfo =>
        {
            objectManager.TryGetId(formationInfo.Captain, out var captainId);

            var heroTroopIds = (formationInfo.HeroTroops ?? Array.Empty<Hero>())
                .Select(heroTroop => objectManager.TryGetId(heroTroop, out var heroTroopId) ? heroTroopId : null)
                .Where(heroTroopId => heroTroopId != null)
                .ToArray();

            return new OrderOfBattleFormation(
                captainId,
                heroTroopIds,
                formationInfo.FormationClass,
                formationInfo.PrimaryClassWeight,
                formationInfo.SecondaryClassWeight,
                formationInfo.Filters);
        }).ToArray();
    }

    public void LoadFormations(OrderOfBattleData orderOfBattle)
    {
        var behavior = Campaign.Current.GetCampaignBehavior<OrderOfBattleCampaignBehavior>();

        // Replaces whatever the server save held, which isn't this player's order of battle
        behavior._fieldBattleFormationInfos = CreateFormationInfos(orderOfBattle?.FieldBattleFormations);
        behavior._fieldBattleArmyFormationInfos = CreateFormationInfos(orderOfBattle?.FieldBattleArmyFormations);
        behavior._siegeFormationInfos = CreateFormationInfos(orderOfBattle?.SiegeFormations);
        behavior._siegeArmyFormationInfos = CreateFormationInfos(orderOfBattle?.SiegeArmyFormations);
    }

    public void SetPlayerFormations(string playerHeroId, OrderOfBattleFormation[] formations, bool isSiegeBattle, bool isInArmy)
    {
        if (!OrderOfBattlePlayerData.PlayerOrderOfBattles.TryGetValue(playerHeroId, out var orderOfBattle))
        {
            orderOfBattle = new OrderOfBattleData();
            OrderOfBattlePlayerData.PlayerOrderOfBattles[playerHeroId] = orderOfBattle;
        }

        if (isSiegeBattle)
        {
            if (isInArmy) orderOfBattle.SiegeArmyFormations = formations;
            else orderOfBattle.SiegeFormations = formations;
        }
        else
        {
            if (isInArmy) orderOfBattle.FieldBattleArmyFormations = formations;
            else orderOfBattle.FieldBattleFormations = formations;
        }
    }

    private List<OrderOfBattleFormationData> CreateFormationInfos(OrderOfBattleFormation[] formations)
    {
        // Unresolved hero troops are dropped because vanilla LoadConfiguration dereferences every one
        return (formations ?? Array.Empty<OrderOfBattleFormation>()).Select(formation =>
        {
            objectManager.TryGetObject<Hero>(formation.CaptainId, out var captain);

            var heroTroops = (formation.HeroTroopIds ?? Array.Empty<string>())
                .Select(heroTroopId => objectManager.TryGetObject<Hero>(heroTroopId, out var heroTroop) ? heroTroop : null)
                .Where(heroTroop => heroTroop != null)
                .ToArray();

            return new OrderOfBattleFormationData(
                captain,
                heroTroops,
                formation.FormationClass,
                formation.PrimaryClassWeight,
                formation.SecondaryClassWeight,
                formation.Filters ?? new());
        }).ToList();
    }
}
