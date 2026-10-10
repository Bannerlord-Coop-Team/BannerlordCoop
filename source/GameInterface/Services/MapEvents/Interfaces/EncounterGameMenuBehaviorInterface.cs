using Common.Logging;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Interfaces;

public interface IEncounterGameMenuBehaviorInterface : IGameAbstraction
{
    void LeftSoldiersBehindConsequence(EncounterGameMenuBehavior behavior, Hero mainHero, MobileParty mainParty);
}

public class EncounterGameMenuBehaviorInterface : IEncounterGameMenuBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<EncounterGameMenuBehaviorInterface>();

    private readonly IDefaultTroopSacrificeModelInterface defaultTroopSacrificeModelInterface;

    public EncounterGameMenuBehaviorInterface(IDefaultTroopSacrificeModelInterface defaultTroopSacrificeModelInterface)
    {
        this.defaultTroopSacrificeModelInterface = defaultTroopSacrificeModelInterface;
    }

    public void LeftSoldiersBehindConsequence(EncounterGameMenuBehavior behavior, Hero mainHero, MobileParty mainParty)
    {
        var mapEvent = mainParty.MapEvent;
        if (mapEvent == null)
        {
            Logger.Error("Unable to leave soldiers behind for {Party} because it is not in a map event", mainParty.StringId);
            return;
        }

        BattleSideEnum playerSide = mainParty.Party.Side;
        int numberOfTroopsSacrificed = defaultTroopSacrificeModelInterface.GetNumberOfTroopsSacrificedForTryingToGetAway(mainHero, mainParty, playerSide, mapEvent);

        RemoveTroopsForTryToGetAway(behavior, mainParty, numberOfTroopsSacrificed);
        CalculateAndRemoveItemsForTryToGetAway(mainParty);
        if (mainParty.IsCurrentlyAtSea)
        {
            Campaign.Current.Models.TroopSacrificeModel.GetShipsToSacrificeForTryingToGetAway(playerSide, mapEvent, out var shipsToCapture, out var shipToTakeDamage, out var damageToApplyForLastShip);
            if (shipsToCapture.Any())
            {
                CaptureShipsForTryToGetAway(playerSide, mapEvent, shipsToCapture);
            }
            if (shipToTakeDamage != null)
            {
                DamageLastShipToTakeTryToGetAway(shipToTakeDamage, damageToApplyForLastShip);
            }
        }

        // Replace CampaignEventDispatcher.Instance.OnPlayerDesertedBattle call.
        // Receivers use main party which isn't available on the server.
        SkillLevelingManager.OnTacticsUsed(mainParty, numberOfTroopsSacrificed * 50f);

        // TODO: Traits aren't synced yet
        //TraitLevelingHelper.OnTroopsSacrificed();

        // TODO: Send message for CompanionGrievanceBehavior.OnPlayerDesertedBattle

        if (mainParty.BesiegerCamp != null)
        {
            mainParty.BesiegerCamp = null;
        }
    }

    private void RemoveTroopsForTryToGetAway(EncounterGameMenuBehavior behavior, MobileParty mainParty, int numberOfTroopsToRemove)
    {
        int numRegularMembers = mainParty.Party.NumberOfRegularMembers;
        if (mainParty.Army != null)
        {
            foreach (MobileParty attachedParty in mainParty.Army.LeaderParty.AttachedParties)
            {
                numRegularMembers += attachedParty.Party.NumberOfRegularMembers;
            }
        }

        float sacrificeRatio = (float)numberOfTroopsToRemove / numRegularMembers;
        behavior.SacrificeTroopsWithRatio(mainParty, sacrificeRatio);
        if (mainParty.Army != null)
        {
            foreach (MobileParty attachedParty in mainParty.Army.LeaderParty.AttachedParties)
            {
                behavior.SacrificeTroopsWithRatio(attachedParty, sacrificeRatio);
            }
        }
    }

    private void CalculateAndRemoveItemsForTryToGetAway(MobileParty mainParty)
    {
        foreach (ItemRosterElement itemRosterElement in new ItemRoster(mainParty.ItemRoster))
        {
            if (!itemRosterElement.EquipmentElement.Item.NotMerchandise && !itemRosterElement.EquipmentElement.Item.IsBannerItem)
            {
                int amountToRemove = MathF.Floor(itemRosterElement.Amount * 0.15f);
                mainParty.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -amountToRemove);
            }
        }
    }

    private void CaptureShipsForTryToGetAway(BattleSideEnum playerSide, MapEvent mapEvent, MBList<Ship> shipsToCapture)
    {
        MBReadOnlyList<MapEventParty> winnerParties = mapEvent.PartiesOnSide(playerSide.GetOppositeSide());
        foreach (KeyValuePair<Ship, MapEventParty> item in Campaign.Current.Models.BattleRewardModel.DistributeDefeatedPartyShipsAmongWinners(mapEvent, shipsToCapture, winnerParties))
        {
            if (item.Value != null)
            {
                ChangeShipOwnerAction.ApplyByLooting(item.Value.Party, item.Key);
            }
            else
            {
                DestroyShipAction.Apply(item.Key);
            }
        }
    }

    private void DamageLastShipToTakeTryToGetAway(Ship ship, float damageToApply)
    {
        ship.OnShipDamaged(damageToApply, null, out _);
    }
}
