using Common;
using Common.Util;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Inventory;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(TeleportationCampaignBehavior))]
internal class DisableTeleportationCampaignBehavior
{
    [HarmonyPatch(nameof(TeleportationCampaignBehavior.RegisterEvents))]
    static bool RegisterEventsPrefix() => ModInformation.IsServer;

    // Disable these methods until they are needed. Currently not patched to handle checks for Clan.PlayerClan and could cause issues for clients
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(TeleportationCampaignBehavior), nameof(TeleportationCampaignBehavior.DailyTickParty)),
        AccessTools.Method(typeof(TeleportationCampaignBehavior), nameof(TeleportationCampaignBehavior.OnHeroComesOfAge)),
        AccessTools.Method(typeof(TeleportationCampaignBehavior), nameof(TeleportationCampaignBehavior.OnPartyDisbandStarted)),
        AccessTools.Method(typeof(TeleportationCampaignBehavior), nameof(TeleportationCampaignBehavior.RemoveTeleportationData))
    };

    static bool Prefix()
    {
        return false;
    }
}
