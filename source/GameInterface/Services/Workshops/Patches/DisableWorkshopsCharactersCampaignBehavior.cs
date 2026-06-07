using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(WorkshopsCharactersCampaignBehavior))]
internal class DisableWorkshopsCharactersCampaignBehavior
{
    [HarmonyPatch(nameof(WorkshopsCharactersCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
