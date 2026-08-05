using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Standing-lesson re-check finding (the exact nuance flagged going into this task, independently re-verified
/// against the decompiled source rather than trusted): unlike Smugglers'/Caravan Ambush's parties,
/// <c>_guardsParty</c> is NOT created via an accept-time dialogue Consequence - it's created by the private
/// <c>CreateGuardsParty()</c>, called ONLY from <c>OnSettlementEnter()</c>, wired via <c>RegisterEvents()</c>.
/// <c>RegisterEvents()</c>-wired listeners are usually inert on a non-owner's mirror because the state they
/// depend on is never populated outside the accepter-only path (design doc §5 Category A) - but that reasoning
/// does NOT hold here: <c>OnSettlementEnter</c>'s own gate (<c>party == MobileParty.MainParty</c>) is trivially
/// satisfiable by ANY peer's own local player entering the SAME settlement, and its whole job is to POPULATE
/// <c>_guardsParty</c> in the first place. Two real gaps follow, both closed here:
///
/// (1) A NON-owner peer's own settlement-entry could create (or, once <c>_guardsParty</c> is force-mirrored to
/// every peer by this same patch's Postfix, crash trying to reference) a guards party belonging to someone
/// else's already-accepted quest. Fixed by blocking <c>OnSettlementEnter</c> ENTIRELY for anyone who isn't the
/// recorded owner - the "gate the actual mutation" principle applied to the one listener that both mutates AND
/// crash-derefs in the same synchronous call.
///
/// (2) If the genuine OWNER is a remote CLIENT, <c>CreateGuardsParty()</c>'s call to
/// <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c> hits the same "CustomPartyComponent ctor
/// hard-blocked on a client" bug Smugglers/Caravan Ambush both had
/// (<see cref="GameInterface.Services.PartyComponents.Patches.Lifetime.CustomPartyComponentLifetimePatches"/>).
/// Unlike those two, this method's ENTIRE body reads only already-shared, deterministic state (never
/// <c>MobileParty.MainParty</c>) - see <see cref="IGangLeaderNeedsWeaponsIssueInterface"/>'s type doc comment -
/// so the server can reproduce it byte-for-byte with ZERO captured/forwarded accepter-derived value, a first
/// among this family's party-spawn gates.
///
/// The remaining wrinkle: vanilla's <c>OnSettlementEnter</c> doesn't just ensure the party exists - in the SAME
/// call it immediately opens a conversation with the guard leader, dereferencing <c>_guardsParty.Party</c>
/// unconditionally right after. On an owner-client whose call gets blocked-and-forwarded, <c>_guardsParty</c> is
/// still null at that point (the real construction hasn't round-tripped yet) - so this gate does NOT simply
/// forward-and-return-true; it fully takes over the decision and lets
/// <see cref="Handlers.GangLeaderNeedsWeaponsIssueHandler"/> finish the conversation-open once the server's
/// broadcast lands (see <see cref="IGangLeaderNeedsWeaponsIssueInterface.OpenGuardConversationIfPossible"/>'s
/// doc comment - a documented, accepted, millisecond-scale timing divergence).
/// </summary>
[HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest))]
internal class GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch
{
    [HarmonyPatch("OnSettlementEnter")]
    [HarmonyPrefix]
    private static bool OnSettlementEnterPrefix(
        GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance,
        MobileParty party, Settlement settlement, Hero hero)
    {
        var owner = __instance.QuestGiver;
        if (owner == null) return true;

        // Non-owner mirror: never let THIS peer's own settlement-entry create (or interact with) a guards party
        // that belongs to someone else's already-accepted quest - see the type doc comment, finding (1).
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner)) return false;

        // Real owner, host/server: vanilla runs completely unmodified - CreateGuardsParty()'s
        // CustomPartyComponent construction is never blocked here, so this is zero-divergence, single-player-
        // equivalent behavior.
        if (!ModInformation.IsClient) return true;

        ContainerProvider.TryResolve<IGangLeaderNeedsWeaponsIssueInterface>(out var issueInterface);
        if (issueInterface == null) return false;

        // The guards party already exists on this owner-client's own mirror (force-mirrored from an earlier
        // request) - CreateGuardsParty() is gated behind `_guardsParty == null` in vanilla's own body, so it
        // will never be reached; vanilla's own conversation-open logic is completely safe to run unmodified.
        if (issueInterface.TryCaptureGuardsParty(owner, out _)) return true;

        // Guards party doesn't exist yet - vanilla's body WOULD try (and fail) to construct it here. Replicate
        // vanilla's own top-level gating condition first, so an entry that wouldn't have done anything anyway
        // (wrong settlement, not enough weapons collected yet, in an army, etc.) doesn't generate needless
        // network traffic.
        if (!issueInterface.ShouldTriggerGuardEncounter(__instance, party, settlement)) return false;

        MessageBroker.Instance.Publish(__instance, new GangLeaderNeedsWeaponsGuardsPartySpawnRequested(owner));
        return false;
    }

    [HarmonyPatch("CreateGuardsParty")]
    [HarmonyPostfix]
    private static void CreateGuardsPartyPostfix(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest __instance)
    {
        if (ModInformation.IsClient) return;

        // Genuine server-side creation - either the host itself is the owner (vanilla's own OnSettlementEnter
        // ran this unmodified) or the server is servicing a client's forwarded spawn request via
        // IGangLeaderNeedsWeaponsIssueInterface.CreateGuardsPartyOnServer - both paths reach this SAME real,
        // Harmony-postfixed method, so one broadcast point covers both.
        ContainerProvider.TryResolve<IGangLeaderNeedsWeaponsIssueInterface>(out var issueInterface);
        if (issueInterface == null) return;

        var owner = __instance.QuestGiver;
        if (!issueInterface.TryCaptureGuardsParty(owner, out var guardsParty)) return;

        MessageBroker.Instance.Publish(__instance, new GangLeaderNeedsWeaponsGuardsPartySpawned(owner, guardsParty));
    }
}
