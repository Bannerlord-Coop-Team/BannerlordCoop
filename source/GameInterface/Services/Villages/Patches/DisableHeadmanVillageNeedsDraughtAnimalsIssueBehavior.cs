using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior))]
internal class DisableHeadmanVillageNeedsDraughtAnimalsIssueBehavior
{
    [HarmonyPatch(nameof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
