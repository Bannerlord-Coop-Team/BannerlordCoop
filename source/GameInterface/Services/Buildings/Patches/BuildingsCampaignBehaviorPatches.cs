using Common;
using Common.Messaging;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace GameInterface.Services.Buildings.Patches;

[HarmonyPatch(typeof(BuildingsCampaignBehavior))]
internal class BuildingsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(BuildingsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(BuildingsCampaignBehavior.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnSettlementOwnerChangedPrefix(Hero newOwner)
    {
        return newOwner.Clan == null || !newOwner.Clan.IsPlayerClan();
    }

    [HarmonyPatch(nameof(BuildingsCampaignBehavior.DailyTickSettlement))]
    [HarmonyPrefix]
    public static bool DailyTickSettlementPrefix(BuildingsCampaignBehavior __instance, Settlement settlement)
    {
        // Skip if village
        if (!settlement.IsFortification) return false;

        // Replace tick for player clans to not control building queues automatically
        if (settlement.OwnerClan.IsPlayerClan())
        {
            Town town = settlement.Town;
            foreach (Building building in town.Buildings)
            {
                if (town.Owner.Settlement.SiegeEvent == null)
                {
                    building.HitPointChanged(10f);
                }
            }
            if (!town.CurrentBuilding.BuildingType.IsDailyProject)
            {
                __instance.TickCurrentBuildingForTown(town);

                return false;
            }
            if (town.Governor != null && town.Governor.GetPerkValue(DefaultPerks.Charm.Virile) && MBRandom.RandomFloat <= DefaultPerks.Charm.Virile.SecondaryBonus)
            {
                Hero randomElement = settlement.Notables.GetRandomElement<Hero>();
                if (randomElement != null)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(town.Governor.Clan.Leader, randomElement, 1, false);
                }
            }

            // Prevent running original which has a Clan.PlayerClan check
            return false;
        }

        // Safe to run normally for non-player clans
        return true;
    }

    [HarmonyPatch(nameof(BuildingsCampaignBehavior.DailyTickSettlement))]
    [HarmonyPostfix]
    public static void DailyTickSettlementPostfix(BuildingsCampaignBehavior __instance, Settlement settlement)
    {
        if (!settlement.IsFortification || !settlement.OwnerClan.IsPlayerClan()) return;

        // Update client's settlement management screen with latest tick
        // Vanilla is normally paused on this screen so ticks won't update normally
        var message = new RefreshPlayerSettlementManagementVM(settlement.Town);
        MessageBroker.Instance.Publish(__instance, message);
    }
}