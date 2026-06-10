using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Patches for player encounters
/// </summary>

[HarmonyPatch(typeof(EncounterManager))]
internal class EncounterManagerPatches
{
    private static ILogger Logger = LogManager.GetLogger<EncounterManagerPatches>();

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.StartSettlementEncounter))]
    private static bool Prefix(MobileParty attackerParty, Settlement settlement)
    {
        if (ModInformation.IsServer) return true;

        if (!attackerParty.IsControlledByThisInstance())
            return false;

        var message = new StartSettlementEncounterAttempted(attackerParty, settlement);
        MessageBroker.Instance.Publish(attackerParty, message);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.HandleEncounterForMobileParty))]
    internal static bool HandleEncounterForMobilePartyPatch(ref MobileParty mobileParty, ref float dt)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Skip this method if party is not controlled
        if (!mobileParty.IsControlledByThisInstance())
            return false;

        return true;
    }

    // EncounterManager.RestartPlayerEncounter is private; patch by name. It is the path that opens the encounter
    // menu/conversation (it calls PlayerEncounter.Current.Init). Parameter order here is (attacker, defender).
    [HarmonyPatch("RestartPlayerEncounter")]
    [HarmonyPrefix]
    private static bool RestartPlayerEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        // Our own server-approved re-run (AllowedThread) runs the real method.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // The server runs it locally (authoritative), unless the targeted party is held in a conversation by
        // another player.
        if (ModInformation.IsServer) return ConversationPartyHold.CanHostRestartEncounter(attackerParty, defenderParty);

        // Client: gate the encounter restart behind server approval (rate-limited + validated in
        // ConversationRequestHandler). On approval the handler re-runs this exact method under an AllowedThread.
        MessageBroker.Instance.Publish(null, new ConversationRequested(defenderParty, attackerParty, forcePlayerOutFromSettlement: false, ConversationRestartSource.EncounterManager));

        return false;
    }

    // After the host's encounter (re)starts via EncounterManager, hold the encountered AI party (see
    // PlayerEncounterPatches.RestartPlayerEncounterPostfix for why this is a postfix).
    [HarmonyPatch("RestartPlayerEncounter")]
    [HarmonyPostfix]
    private static void RestartPlayerEncounterPostfix()
    {
        if (!ModInformation.IsServer) return;

        ConversationPartyHold.EngageHostEncounteredParty();
    }
}

