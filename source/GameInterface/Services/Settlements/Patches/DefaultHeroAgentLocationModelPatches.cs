using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(DefaultHeroAgentLocationModel))]
internal class DefaultHeroAgentLocationModelPatches
{
    [HarmonyPatch(nameof(DefaultHeroAgentLocationModel.WillBeListedInOverlay))]
    [HarmonyPrefix]
    private static bool WillBeListedInOverlayPrefix(LocationCharacter locationCharacter, ref bool __result)
    {
        if (ModInformation.IsClient &&
            locationCharacter?.Character?.IsHero == true &&
            locationCharacter.Character.HeroObject?.CharacterObject == null)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
