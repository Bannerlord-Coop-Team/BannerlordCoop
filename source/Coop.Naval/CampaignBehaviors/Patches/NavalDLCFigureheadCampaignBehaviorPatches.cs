using Common;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalDLCFigureheadCampaignBehavior))]
internal class NavalDLCFigureheadCampaignBehaviorPatches
{
    // The server has no player to notify, and each player's last loot time is kept in NavalPlayerData
    [HarmonyPatch(nameof(NavalDLCFigureheadCampaignBehavior.OnFigureheadUnlocked))]
    [HarmonyPrefix]
    private static bool OnFigureheadUnlockedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(NavalDLCFigureheadCampaignBehavior.OnShipOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnShipOwnerChangedPrefix(Ship ship, PartyBase oldOwner)
    {
        if (ModInformation.IsClient) return false;

        var newClan = ship.Owner?.MobileParty?.ActualClan;

        // Replace Clan.PlayerClan usage
        if (!newClan.IsPlayerClan()) return true;

        // Player clan path only change figurehead to be null
        if (ship.CanEquipFigurehead && ship.Figurehead != null && oldOwner?.MobileParty?.ActualClan != newClan)
        {
            ship.ChangeFigurehead(null);
        }

        return false;
    }
}
