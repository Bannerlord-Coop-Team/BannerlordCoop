using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior))]
internal class DisableVillageNeedsToolsIssueBehavior
{
    [HarmonyPatch(nameof(VillageNeedsToolsIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
