using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(DefaultSettlementSecurityModel))]
internal class DisableEffectsOnSecurityPatch
{
    [HarmonyPatch(nameof(DefaultSettlementSecurityModel.CalculateInfestedHideoutEffectsOnSecurity))]
    [HarmonyPrefix]
    public static bool CalculateInfestedHideoutEffectsOnSecurityPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(DefaultSettlementSecurityModel.CalculateIssueEffectsOnSecurity))]
    [HarmonyPrefix]
    public static bool CalculateIssueEffectsOnSecurityPrefix() => false;
}