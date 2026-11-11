using Common;
using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Hideouts.Patches;

/// <summary>Keeps the native hideout clear on the server, which replicates its leaves and visibility.</summary>
[HarmonyPatch(typeof(HideoutEventComponent))]
internal class HideoutEventComponentPatches
{
    // A client reaches this from its own encounter end, the server clears through HideoutCampaignConsequencesHandler.
    [HarmonyPatch(nameof(HideoutEventComponent.OnHideoutCleared))]
    [HarmonyPrefix]
    private static bool OnHideoutClearedPrefix()
    {
        if (ModInformation.IsServer) return true;
        return CallOriginalPolicy.IsOriginalAllowed();
    }
}
