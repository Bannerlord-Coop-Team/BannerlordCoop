using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches;

/// <summary>
/// Publishes visual creation from a server without a map visual manager.
/// </summary>
[HarmonyPatch]
internal class HeadlessPartyVisualLifetimePatches
{
    // The same moment a graphical host builds the visual: the party's registration at the end of
    // the MobileParty constructor — the party is already id-registered (its creation prefix runs
    // at constructor entry) and Party is assigned, so the create handler can resolve both.
    [HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.AddMobileParty))]
    [HarmonyPostfix]
    private static void AddMobilePartyPostfix(MobileParty party)
    {
        if (ModInformation.IsClient || MobilePartyVisualManager.Current != null || CallOriginalPolicy.IsOriginalAllowed()) return;

        var partyBase = party?.Party;
        if (partyBase == null) return;
        MessageBroker.Instance.Publish(party, new PartyVisualCreated(null, partyBase));
    }
}
