using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GangLeaderNeedsRecruitsIssueBehavior))]
internal class DisableGangLeaderNeedsRecruitsIssueBehavior
{
    [HarmonyPatch(nameof(GangLeaderNeedsRecruitsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
