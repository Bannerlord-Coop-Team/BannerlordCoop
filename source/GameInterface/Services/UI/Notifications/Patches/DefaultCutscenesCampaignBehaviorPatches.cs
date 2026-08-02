using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(DefaultCutscenesCampaignBehavior), nameof(DefaultCutscenesCampaignBehavior.OnHeroesMarried))]
internal static class DefaultCutscenesCampaignBehaviorPatches
{
    [HarmonyPrefix]
    private static bool OnHeroesMarriedPrefix()
    {
        return !ModInformation.IsServer;
    }
}
