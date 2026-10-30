using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

[HarmonyPatch(typeof(SiegeEvent))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class SiegeBlockadePatches
{
    [HarmonyPatch(nameof(SiegeEvent.ActivateBlockade))]
    [HarmonyPostfix]
    private static void ActivateBlockadePostfix(SiegeEvent __instance) => OnBlockadeChanged(__instance);

    [HarmonyPatch(nameof(SiegeEvent.DeactivateBlockade))]
    [HarmonyPostfix]
    private static void DeactivateBlockadePostfix(SiegeEvent __instance) => OnBlockadeChanged(__instance);

    private static void OnBlockadeChanged(SiegeEvent siegeEvent)
    {
        if (ModInformation.IsClient) return;

        // Replace MobileParty.MainParty usage, same parties as BesiegerCamp.IsBesiegerSideParty
        var besiegerParties = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType()
            .SelectMany(party => party.MobileParty.AttachedParties.Prepend(party.MobileParty));
        foreach (var party in besiegerParties)
        {
            if (party.IsPlayerParty()) party.Anchor.IsDisabled = siegeEvent.IsBlockadeActive;
        }

        MessageBroker.Instance.Publish(siegeEvent, new SiegeBlockadeChanged(siegeEvent));
    }
}

[HarmonyPatch(typeof(MobileParty))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class MobilePartyBlockadeAnchorPatches
{
    // Replace MobileParty.IsMainParty usage so a player joining a blockading siege can't use their anchor
    [HarmonyPatch(nameof(MobileParty.OnPartyJoinedSiegeInternal))]
    [HarmonyPostfix]
    private static void OnPartyJoinedSiegeInternalPostfix(MobileParty __instance)
    {
        if (ModInformation.IsClient) return;

        if (__instance.IsPlayerParty() && __instance.SiegeEvent.IsBlockadeActive) __instance.Anchor.IsDisabled = true;
    }

    // Prefix because vanilla reads the blockade before the party leaves, and leaving can lift it
    [HarmonyPatch(nameof(MobileParty.OnPartyLeftSiegeInternal))]
    [HarmonyPrefix]
    private static void OnPartyLeftSiegeInternalPrefix(MobileParty __instance)
    {
        if (ModInformation.IsClient) return;

        if (__instance.IsPlayerParty() && __instance.SiegeEvent.IsBlockadeActive) __instance.Anchor.IsDisabled = false;
    }
}
