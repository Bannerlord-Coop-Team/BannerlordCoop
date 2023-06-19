using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Arenas.Patches;

[HarmonyPatch(typeof(BettingFraudIssueBehavior))]
internal class DisableBettingFraudIssueBehavior
{
    [HarmonyPatch(nameof(BettingFraudIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
