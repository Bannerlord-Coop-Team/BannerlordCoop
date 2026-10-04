using Common;
using Common.Messaging;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches;

/// <summary>
/// Publishes client-only party visual lifetime from a server without a map visual manager.
/// </summary>
[HarmonyPatch]
internal class HeadlessPartyVisualLifetimePatches
{
    private static bool IsHeadlessServer => ModInformation.IsServer && MobilePartyVisualManager.Current == null;

    // The same moment a graphical host builds the visual: the party's registration at the end of
    // the MobileParty constructor — the party is already id-registered (its creation prefix runs
    // at constructor entry) and Party is assigned, so the create handler can resolve both.
    [HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.AddMobileParty))]
    [HarmonyPostfix]
    private static void AddMobilePartyPostfix(MobileParty party)
    {
        if (!IsHeadlessServer) return;

        var partyBase = party?.Party;
        if (partyBase == null) return;
        MessageBroker.Instance.Publish(party, new PartyVisualCreated(null, partyBase));
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.RemoveParty))]
    [HarmonyPostfix]
    private static void RemovePartyPostfix(MobileParty __instance)
    {
        if (!IsHeadlessServer) return;

        var partyBase = __instance?.Party;
        if (partyBase == null) return;
        MessageBroker.Instance.Publish(__instance, new PartyVisualDestroyed(null, __instance));
    }
}
