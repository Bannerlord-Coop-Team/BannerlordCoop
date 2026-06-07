using HarmonyLib;
using SandBox.Issues;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(SnareTheWealthyIssueBehavior))]
internal class DisableSnareTheWealthyIssueBehavior
{
    [HarmonyPatch(nameof(SnareTheWealthyIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
