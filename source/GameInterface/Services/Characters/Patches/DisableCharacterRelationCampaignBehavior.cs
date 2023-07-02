using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(CharacterRelationCampaignBehavior))]
internal class DisableCharacterRelationCampaignBehavior
{
    [HarmonyPatch(nameof(CharacterRelationCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
