using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior))]
internal class DisableTheConquestOfSettlementIssueBehavior
{
    [HarmonyPatch(nameof(TheConquestOfSettlementIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
