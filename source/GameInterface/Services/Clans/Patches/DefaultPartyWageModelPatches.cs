using GameInterface;
using GameInterface.Services.Clans.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

[HarmonyPatch(typeof(DefaultPartyWageModel))]
internal static class DefaultPartyWageModelPatches
{
    [HarmonyPatch(nameof(DefaultPartyWageModel.GetTotalWage))]
    [HarmonyPrefix]
    public static bool GetTotalWagePrefix(DefaultPartyWageModel __instance, ref ExplainedNumber __result, MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
    {
        ContainerProvider.TryResolve<IDefaultPartyWageModelInterface>(out var partyWageModelInterface);

        __result = partyWageModelInterface.GetTotalWage(__instance, mobileParty, troopRoster, includeDescriptions);

        return false;
    }
}