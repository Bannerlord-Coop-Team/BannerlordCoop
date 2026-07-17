using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Players;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
        if (IsPendingParty(attackerParty?.Party))
            return false;

        if (RaidAiInterventionSuppression.ShouldSuppressSettlementEncounter(attackerParty, settlement))
            return false;

        if (TryRequestActiveSlowRaidConversation(attackerParty, settlement))
            return false;

        if (ModInformation.IsServer) return true;

        if (!attackerParty.IsControlledByThisInstance())
            return false;

        var message = new StartSettlementEncounterAttempted(attackerParty, settlement);
        MessageBroker.Instance.Publish(attackerParty, message);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    private static bool StartPartyEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (IsPendingParty(attackerParty) || IsPendingParty(defenderParty))
            return false;

        if (IsAwaitingBattleMissionExit(attackerParty) || IsAwaitingBattleMissionExit(defenderParty))
            return false;

        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (TryRequestActiveSlowRaidSettlementEncounter(attackerParty, defenderParty))
            return false;

        return !RaidAiInterventionSuppression.ShouldSuppressEncounter(attackerParty, defenderParty);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.HandleEncounterForMobileParty))]
    internal static bool HandleEncounterForMobilePartyPatch(ref MobileParty mobileParty, ref float dt)
    {
        if (IsPendingParty(mobileParty?.Party))
            return false;

        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsServer && RaidAiInterventionSuppression.ShouldSuppressMobilePartyEncounter(mobileParty))
            return false;

        // Skip this method if party is not controlled
        if (!mobileParty.IsControlledByThisInstance())
            return false;

        // Client parties can tick during startup before their AI targets are initialized or registered.
        // Guard that incomplete state even though behavior snapshots themselves apply atomically.
        if (ModInformation.IsClient)
        {
            if (mobileParty.Ai.AiBehaviorInteractable == null)
                return false;

            if (mobileParty.ShortTermBehavior == AiBehavior.EngageParty && mobileParty.ShortTermTargetParty == null)
                return false;
        }

        return true;
    }

    internal static bool IsPendingParty(PartyBase party) =>
        PendingMapEventPartyMovementPatch.IsPending(party);

    internal static bool IsAwaitingBattleMissionExit(PartyBase party)
    {
        if (ModInformation.IsClient || party?.MapEvent != null || party?.MobileParty == null)
            return false;

        if (!PlayerManager.TryGetControlledObjectInfo(party.MobileParty, out var controlledObject))
            return false;

        return ContainerProvider.TryResolve<IBattleHostRegistry>(out var hostRegistry)
            && hostRegistry.IsControllerAssigned(controlledObject.ObjectControllerId);
    }

    // EncounterManager.RestartPlayerEncounter is private; patch by name. It is the path that opens the encounter
    // menu/conversation (it calls PlayerEncounter.Current.Init). Parameter order here is (attacker, defender).
    [HarmonyPatch("RestartPlayerEncounter")]
    [HarmonyPrefix]
    private static bool RestartPlayerEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (IsPendingParty(attackerParty) || IsPendingParty(defenderParty))
            return false;

        // Our own server-approved re-run (AllowedThread) runs the real method.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (RaidAiInterventionSuppression.ShouldSuppressEncounter(attackerParty, defenderParty))
            return false;

        if (TryRequestActiveSlowRaidSettlementEncounter(attackerParty, defenderParty))
            return false;

        // The server runs it locally (authoritative).
        if (ModInformation.IsServer) return true;

        // Client: gate the encounter restart behind server approval (rate-limited + validated in
        // ConversationRequestHandler). On approval the handler re-runs this exact method under an AllowedThread.
        MessageBroker.Instance.Publish(null, new ConversationRequested(defenderParty, attackerParty, forcePlayerOutFromSettlement: false, ConversationRestartSource.EncounterManager));

        return false;
    }

    private static bool TryRequestActiveSlowRaidConversation(MobileParty attackerParty, Settlement settlement)
    {
        if (ModInformation.IsServer)
            return false;

        if (attackerParty?.IsControlledByThisInstance() != true)
            return false;

        var defenderParty = settlement?.Party;
        if (defenderParty?.MapEvent?.IsActiveSlowVillageRaid() != true)
            return false;

        MessageBroker.Instance.Publish(null, new ConversationRequested(defenderParty, attackerParty.Party, false, ConversationRestartSource.EncounterManager));
        return true;
    }

    private static bool TryRequestActiveSlowRaidSettlementEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ModInformation.IsServer)
            return false;

        var mobileParty = attackerParty?.MobileParty;
        if (mobileParty?.IsControlledByThisInstance() != true)
            return false;

        var mapEvent = GetActiveSlowRaidMapEvent(defenderParty) ?? GetActiveSlowRaidMapEvent(attackerParty);
        var settlement = mapEvent?.MapEventSettlement;
        if (settlement == null)
            return false;

        MessageBroker.Instance.Publish(mobileParty, new StartSettlementEncounterAttempted(mobileParty, settlement));
        return true;
    }

    private static MapEvent GetActiveSlowRaidMapEvent(PartyBase party)
    {
        var mapEvent = party?.MapEvent;
        return mapEvent.IsActiveSlowVillageRaid() ? mapEvent : null;
    }
}

