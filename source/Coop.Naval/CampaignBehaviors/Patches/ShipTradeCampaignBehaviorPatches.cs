using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(ShipTradeCampaignBehavior))]
internal class ShipTradeCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ShipTradeCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    public static bool DailyTickClanPrefix(Clan clan)
    {
        return ModInformation.IsServer && !clan.IsPlayerClan();
    }

    [HarmonyPatch(nameof(ShipTradeCampaignBehavior.OnShipOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnShipOwnerChangedPrefix(Ship ship, PartyBase oldOwner, ChangeShipOwnerAction.ShipOwnerChangeDetail details)
    {
        if (ModInformation.IsClient) return false;

        if (details != ChangeShipOwnerAction.ShipOwnerChangeDetail.ApplyByTrade) return true;

        Hero governor = null;
        if (oldOwner.IsSettlement)
        {
            governor = oldOwner.Settlement.Town.Governor;
        }
        else if (ship.Owner.IsSettlement)
        {
            governor = ship.Owner.Settlement.Town.Governor;
        }

        // Replace !Hero.MainHero guard to work for coop
        return !IsPlayerSelfDealing(governor, ship);
    }

    [HarmonyPatch(nameof(ShipTradeCampaignBehavior.OnShipRepaired))]
    [HarmonyPrefix]
    public static bool OnShipRepairedPrefix(Ship ship, Settlement repairPort)
    {
        if (ModInformation.IsClient) return false;

        // Replace !Hero.MainHero guard to work for coop
        return !IsPlayerSelfDealing(repairPort?.Town?.Governor, ship);
    }

    [HarmonyPatch(nameof(ShipTradeCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ShipTradeCampaignBehavior.OnGameLoadFinished))]
    [HarmonyPrefix]
    public static bool OnGameLoadFinishedPrefix() => ModInformation.IsServer;

    private static bool IsPlayerSelfDealing(Hero governor, Ship ship)
    {
        return governor != null
            && governor.IsPlayerHero()
            && ship.Owner?.LeaderHero == governor;
    }
}
