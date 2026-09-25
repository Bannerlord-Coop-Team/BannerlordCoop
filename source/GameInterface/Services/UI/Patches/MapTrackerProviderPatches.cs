using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Messages;
using HarmonyLib;
using SandBox.ViewModelCollection.Map.Tracker;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class MapTrackerProviderUpdatePatches
{
    [HarmonyPatch(typeof(MapMobilePartyTrackItemVM), nameof(MapMobilePartyTrackItemVM.IsVisibleOnMap))]
    [HarmonyPostfix]
    public static void IsVisibleOnMapPostfix(MapMobilePartyTrackItemVM __instance, ref bool __result)
    {
        __result &= __instance.TrackedObject.IsActive;
    }

    [HarmonyPatch(typeof(MapTrackerProvider), nameof(MapTrackerProvider.CanAddMobileParty))]
    [HarmonyPostfix]
    public static void CanAddMobilePartyPostfix(MobileParty party, ref bool __result)
    {
        // Defeated player parties are retained for release, even after their leader is removed.
        if (__result && party.IsPlayerParty() && (party.LeaderHero == null || party.LeaderHero.IsPrisoner))
            __result = false;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMobilePartyCreated))]
    [HarmonyPostfix]
    public static void OnMobilePartyCreatedPostfix(MobileParty party)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyCreated(party));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnPartyDisbanded))]
    [HarmonyPostfix]
    public static void OnPartyDisbandedPostfix(MobileParty disbandParty, Settlement relatedSettlement)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyRemoved(disbandParty));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    public static void OnMobilePartyDestroyedPostfix(MobileParty mobileParty, PartyBase destroyerParty)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyRemoved(mobileParty));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanCreated))]
    [HarmonyPostfix]
    public static void OnCompanionClanCreatedPostfix(Clan clan, bool isCompanion)
    {
        if (ModInformation.IsClient) return;
        if (!isCompanion || clan.Leader.PartyBelongedTo == null) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyRemoved(clan.Leader.PartyBelongedTo));
    }
}
