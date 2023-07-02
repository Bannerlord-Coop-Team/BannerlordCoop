using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(CharacterDevelopmentCampaignBehavior))]
internal class DisableCharacterDevelopmentCampaignBehavior
{
    [HarmonyPatch(nameof(CharacterDevelopmentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
