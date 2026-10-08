using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Missions.Naval;

[HarmonyPatch(typeof(NavalInitializationCampaignBehavior))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalIntroductionPopupPatch
{
    // Set the 6 focus points locally on joining client, which then get sent to the server as part of the packaged hero
    [HarmonyPatch(nameof(NavalInitializationCampaignBehavior.OnCharacterCreationIsOver))]
    [HarmonyPrefix]
    private static void OnCharacterCreationIsOverPrefix(NavalInitializationCampaignBehavior __instance)
    {
        __instance._hasIntroductionPopUpBeenShown = true;
    }
}
