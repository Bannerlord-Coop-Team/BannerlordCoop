using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.AnchorPoints.Patches;

/// <summary>
/// Anchors have no destroy method of their own, their lifetime ends when OnRemoveParty clears MobileParty.Anchor
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class AnchorPointLifetimePatches
{
    [HarmonyPatch(nameof(MobileParty.OnRemoveParty))]
    [HarmonyPrefix]
    private static void OnRemovePartyPrefix(MobileParty __instance, ref AnchorPoint __state)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient) return;

        __state = __instance.Anchor;
    }

    // Runs after Anchor = null is synced and before the party itself is destroyed
    [HarmonyPatch(nameof(MobileParty.OnRemoveParty))]
    [HarmonyPostfix]
    private static void OnRemovePartyPostfix(MobileParty __instance, AnchorPoint __state)
    {
        if (__state == null) return;

        MessageBroker.Instance.Publish(__instance, new InstanceDestroyed<AnchorPoint>(__state));
    }
}
