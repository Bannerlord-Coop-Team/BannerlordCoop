using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GangLeaderNeedsRecruitsIssueBehavior))]
internal class DisableGangLeaderNeedsRecruitsIssueBehavior
{
    [HarmonyPatch(nameof(GangLeaderNeedsRecruitsIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
