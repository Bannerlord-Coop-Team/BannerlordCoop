using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(CharacterRelationCampaignBehavior))]
internal class DisableCharacterRelationCampaignBehavior
{
    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
