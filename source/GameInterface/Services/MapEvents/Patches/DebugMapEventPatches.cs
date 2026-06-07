using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Policies;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class DebugMapEventPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<DebugMapEventPatches>();

    [HarmonyPatch(typeof(TroopUpgradeTracker), nameof(TroopUpgradeTracker.CalculateReadyToUpgradeSafe))]
    [HarmonyPrefix]
    private static bool PrefixBattleState(TroopUpgradeTracker __instance, ref TroopRosterElement el, PartyBase owner, int __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        // Disable on client for now
        if (ModInformation.IsClient)
        {
            __result = 0;
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Init), new Type[] { typeof(PartyBase), typeof(PartyBase), typeof(Settlement) })]
    [HarmonyPrefix]
    private static void Prefix_BattleState(PartyBase attackerParty, PartyBase defenderParty, Settlement settlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        ;
    }
}