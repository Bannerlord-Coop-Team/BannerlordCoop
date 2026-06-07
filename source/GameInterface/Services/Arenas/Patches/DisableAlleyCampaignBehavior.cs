using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Alleys.Patches;

[HarmonyPatch(typeof(ArenaMasterCampaignBehavior))]
internal class DisableArenaMasterCampaignBehavior
{
    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
