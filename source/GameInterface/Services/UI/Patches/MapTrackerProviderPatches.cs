using Common;
using Common.Messaging;
using GameInterface.Services.UI.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(CampaignEventDispatcher))]
internal class MapTrackerProviderUpdatePatches
{
    [HarmonyPatch(nameof(CampaignEventDispatcher.OnMobilePartyCreated))]
    [HarmonyPostfix]
    public static void OnMobilePartyCreatedPostfix(MobileParty party)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyCreated(party));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnPartyDisbanded))]
    [HarmonyPostfix]
    public static void OnPartyDisbandedPostfix(MobileParty disbandParty, Settlement relatedSettlement)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyRemoved(disbandParty));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    public static void OnMobilePartyDestroyedPostfix(MobileParty mobileParty, PartyBase destroyerParty)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyRemoved(mobileParty));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnClanCreated))]
    [HarmonyPostfix]
    public static void OnCompanionClanCreatedPostfix(Clan clan, bool isCompanion)
    {
        if (ModInformation.IsClient) return;
        if (!isCompanion || clan.Leader.PartyBelongedTo == null) return;

        MessageBroker.Instance.Publish(null, new MapTrackerPartyRemoved(clan.Leader.PartyBelongedTo));
    }
}