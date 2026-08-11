using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Kingdoms.Extentions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(FactionDiscontinuationCampaignBehavior))]
internal class FactionDiscontinuationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(FactionDiscontinuationCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return ModInformation.IsServer;
    }
    [HarmonyPatch(nameof(FactionDiscontinuationCampaignBehavior.CanKingdomBeDiscontinued))]
    [HarmonyPrefix]
    private static bool CanKingdomBeDiscontinuedPrefix(FactionDiscontinuationCampaignBehavior __instance, Kingdom kingdom, ref bool __result)
    {
        bool flag = !kingdom.IsEliminated && !kingdom.IsPlayerKingdom() && kingdom.Settlements.IsEmpty<Settlement>();
        if (flag)
        {
            CampaignEventDispatcher.Instance.CanKingdomBeDiscontinued(kingdom, ref flag);
        }
        __result = flag;
        return false;
    }
    [HarmonyPatch(nameof(FactionDiscontinuationCampaignBehavior.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix(FactionDiscontinuationCampaignBehavior __instance, Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (newKingdom == null)
        {
            if (__instance.CanClanBeDiscontinued(clan))
            {
                __instance.AddIndependentClan(clan);
            }
        }
        else if (__instance._independentClans.ContainsKey(clan))
        {
            __instance._independentClans.Remove(clan);
        }
        if (clan.IsPlayerClan() && oldKingdom != null && __instance.CanKingdomBeDiscontinued(oldKingdom))
        {
            __instance.DiscontinueKingdom(oldKingdom);
        }
        return false;
    }
    [HarmonyPatch(nameof(FactionDiscontinuationCampaignBehavior.CanClanBeDiscontinued))]
    [HarmonyPrefix]
    private static bool CanClanBeDiscontinuedPrefix(Clan clan, ref bool __result)
    {
        __result = clan.Kingdom == null && !clan.IsRebelClan && !clan.IsBanditFaction && !clan.IsMinorFaction && !clan.IsPlayerClan() && clan.Settlements.IsEmpty<Settlement>();
        return false;
    }
}
