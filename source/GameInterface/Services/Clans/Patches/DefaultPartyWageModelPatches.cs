using GameInterface.Services.Clans.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Routes party wage totals through <see cref="IDefaultPartyWageModelInterface"/> so every side computes the same wages.
/// </summary>
[HarmonyPatch(typeof(DefaultPartyWageModel))]
internal static class DefaultPartyWageModelPatches
{
    /// <summary>
    /// Replaces the wage total with the coop calculation, running vanilla when no session container is available.
    /// </summary>
    [HarmonyPatch(nameof(DefaultPartyWageModel.GetTotalWage))]
    [HarmonyPrefix]
    public static bool GetTotalWagePrefix(DefaultPartyWageModel __instance, ref ExplainedNumber __result, MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
    {
        if (!ContainerProvider.TryResolve<IDefaultPartyWageModelInterface>(out var partyWageModelInterface)) return true;

        __result = partyWageModelInterface.GetTotalWage(__instance, mobileParty, troopRoster, includeDescriptions);

        return false;
    }
}
