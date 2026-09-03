using Common;
using Common.Messaging;
using GameInterface.Services.UI.Messages;
using HarmonyLib;
using SandBox.ViewModelCollection.Map.Tracker;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(MapTrackerProvider))]
internal class MapTrackerProviderPatches
{
    [HarmonyPatch(nameof(MapTrackerProvider.OnMobilePartyCreated))]
    [HarmonyPrefix]
    public static void OnMobilePartyCreatedPrefix(MapTrackerProvider __instance, MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new MapTrackerPartyCreated(mobileParty));
    }

    [HarmonyPatch(nameof(MapTrackerProvider.OnPartyDisbanded))]
    [HarmonyPrefix]
    public static void OnPartyDisbandedPrefix(MapTrackerProvider __instance, MobileParty disbandedParty)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new MapTrackerPartyRemoved(disbandedParty));
    }

    [HarmonyPatch(nameof(MapTrackerProvider.OnPartyDestroyed))]
    [HarmonyPrefix]
    public static void OnPartyDestroyedPrefix(MapTrackerProvider __instance, MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new MapTrackerPartyRemoved(mobileParty));
    }
}
