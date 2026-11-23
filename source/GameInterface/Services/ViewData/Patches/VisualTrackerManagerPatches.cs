using Common;
using Common.Messaging;
using GameInterface.Services.ViewData.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.ViewData.Patches;

/// <summary>
/// Map pins live in VisualTrackerManager, which every track toggle writes, so the view data is sent when a pin changes
/// </summary>
[HarmonyPatch(typeof(VisualTrackerManager))]
internal class VisualTrackerManagerPatches
{
    [HarmonyPatch(nameof(VisualTrackerManager.RegisterObject))]
    [HarmonyPostfix]
    private static void RegisterObjectPostfix(ITrackableCampaignObject trackableObject)
    {
        PublishIfPin(trackableObject);
    }

    [HarmonyPatch(nameof(VisualTrackerManager.RemoveTrackedObject))]
    [HarmonyPostfix]
    private static void RemoveTrackedObjectPostfix(ITrackableBase trackableObject)
    {
        PublishIfPin(trackableObject);
    }

    private static void PublishIfPin(ITrackableBase trackableObject)
    {
        if (ModInformation.IsClient && (trackableObject is Settlement || trackableObject is MobileParty))
            MessageBroker.Instance.Publish(trackableObject, new ViewDataChanged(Hero.MainHero));
    }
}