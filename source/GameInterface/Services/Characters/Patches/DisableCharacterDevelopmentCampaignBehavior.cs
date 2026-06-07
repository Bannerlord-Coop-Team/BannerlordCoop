using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(CharacterDevelopmentCampaignBehavior))]
internal class DisableCharacterDevelopmentCampaignBehavior
{
    [HarmonyPatch(nameof(CharacterDevelopmentCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
