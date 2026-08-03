using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(DefaultCutscenesCampaignBehavior), nameof(DefaultCutscenesCampaignBehavior.RegisterEvents))]
internal static class DefaultCutscenesCampaignBehaviorPatches
{
    [HarmonyPrefix]
    private static bool RegisterCutScenesPrefix()
    {
        return !ModInformation.IsServer; //Disable cutscenes on server as they are not needed
    }
}
