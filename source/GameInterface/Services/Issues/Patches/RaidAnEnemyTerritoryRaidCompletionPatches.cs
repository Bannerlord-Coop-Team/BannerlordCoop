using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug fix - <c>RaidAnEnemyTerritoryIssueQuest.OnRaidCompleted</c> - the same root-cause pattern as
/// <c>TheConquestOfSettlementIssueQuest.OnSiegeCompleted</c> (see
/// <see cref="TheConquestOfSettlementSiegeCompletionPatches"/>'s doc comment for the full precedent this fix
/// matches).
///
/// Root cause (confirmed by decompile + this repo's own <c>MapEventPatches.Prefix_FinalizeEventAux</c>, gated
/// on <c>ModInformation.IsServer</c> identically to what the Conquest precedent already established for sieges):
/// vanilla's own listener trigger, <c>RaidEventComponent.OnBeforeFinalize</c> -&gt;
/// <c>CampaignEventDispatcher.Instance.RaidCompleted</c>, is only ever reached from
/// <c>MapEvent.FinalizeEventAux()</c>'s real body, which only ever runs on the coop SERVER's own process (a
/// client's own call is intercepted and blocked entirely - see <c>MapEventPatches.Prefix_FinalizeEventAux</c>).
/// Separately, <c>QuestBase.RegisterEvents()</c> - which is what wires this quest's own <c>OnRaidCompleted</c>
/// listener at all - is only ever called from <c>QuestBase.StartQuest()</c>, itself only ever reached from this
/// quest's <c>OfferDialogFlow</c> accept <c>Consequence</c> (a live dialogue callback, i.e. only on the genuine
/// accepter's own machine - confirmed: <c>IssueBase.StartIssueWithQuest()</c>, what the generic accept-mirror
/// replay every OTHER peer uses (<c>IssueManager.StartIssueQuest</c>) actually calls, only ever constructs the
/// Quest object via <c>GenerateIssueQuest</c>, never calls <c>StartQuest()</c>). Net effect: this listener is
/// only EVER reachable at all when the genuine accepter happens to be the server operator - on any other
/// topology it silently never fires, and even in the one reachable case the check itself
/// (<c>mapEvent.IsPlayerMapEvent</c> - <c>this == MobileParty.MainParty?.MapEvent</c> - and
/// <c>mapEvent.PlayerSide</c> - <c>PartyBase.MainParty.Side</c>, both confirmed by decompile) reads "this
/// machine's own locally-controlled avatar," not this quest's real recorded owner - the two only coincide by
/// the accident of the accepter and the server operator being the same connected player.
///
/// Fix (matching the Conquest precedent's shape): the vanilla per-quest-instance listener is neutered entirely
/// (it can never reach the true owner on its own) and replaced by ONE server-side, module-level listener,
/// subscribed once per campaign load, that scans every currently-active
/// <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue"/> for a quest whose real recorded
/// owner (via <see cref="RaidAnEnemyTerritoryIssueOwnershipResolver"/>) genuinely led the winning attacker side
/// of this raid - reachable and correct regardless of whether the real owner is the server operator, a remote
/// client, or (on a dedicated server) nobody locally at all.
///
/// Unlike Conquest's Bug 1 (which only needed to trigger a generically-synced quest FINALIZATION), this fix
/// mutates a mid-quest progress COUNTER (<c>_raidedVillages</c>) that has no generic sync mechanism in this
/// codebase - so the resolved mutation is applied directly to the server's own local quest mirror via
/// <see cref="IRaidAnEnemyTerritoryIssueInterface.TryApplyRaidedVillage"/> AND broadcast to every other peer via
/// <see cref="RaidAnEnemyTerritoryVillageRaided"/>/<see cref="NetworkRaidAnEnemyTerritoryVillageRaided"/> (see
/// <see cref="Handlers.RaidAnEnemyTerritoryIssueHandler"/>) - otherwise the real owner's OWN mirror (whenever
/// they aren't the server) would never see their own progress, and the turn-in dialogue's
/// <c>_raidedVillages.Count &gt;= 3</c> condition would never trip on their own machine.
/// </summary>
[HarmonyPatch]
internal class RaidAnEnemyTerritoryRaidCompletionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidAnEnemyTerritoryRaidCompletionPatches>();

    /// <summary>Neuters vanilla's own per-quest-instance listener entirely - see the type doc comment for why it
    /// can never reliably reach the true owner on its own. All of its logic is replaced by
    /// <see cref="OnRaidCompleted"/> below.</summary>
    [HarmonyPatch(typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest), "OnRaidCompleted")]
    [HarmonyPrefix]
    private static bool OnRaidCompletedPrefix() => false;

    /// <summary>Registered once per campaign load, matching <c>TheConquestOfSettlementSiegeCompletionPatches</c>'
    /// own established "postfix the outer <c>CampaignBehaviorBase.RegisterEvents</c>" pattern. Server-only:
    /// <c>RaidCompletedEvent</c> is only ever actually dispatched on the coop server's own process (see the type
    /// doc comment), so subscribing on a client would just never fire.</summary>
    [HarmonyPatch(typeof(RaidAnEnemyTerritoryIssueBehavior), nameof(RaidAnEnemyTerritoryIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(RaidAnEnemyTerritoryIssueBehavior __instance)
    {
        if (ModInformation.IsClient) return;

        CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(__instance, OnRaidCompleted);
    }

    private static void OnRaidCompleted(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        if (raidEvent == null) return;
        var mapEvent = raidEvent.MapEvent;
        if (mapEvent == null || !mapEvent.IsRaid) return;
        if (mapEvent.MapEventSettlement == null || !mapEvent.MapEventSettlement.IsVillage) return;
        // Same real constraint vanilla's own mapEvent.PlayerSide == winnerSide check encoded (a raid only ever
        // "completes successfully" from the attacker's perspective - a defender victory never reduces the
        // settlement to 0 hit points below), just expressed without reading PartyBase.MainParty.Side.
        if (winnerSide != BattleSideEnum.Attacker) return;
        if (raidEvent.MapEventSettlement.SettlementHitPoints != 0f) return;
        if (Campaign.Current?.IssueManager == null) return;

        // Snapshot first: a genuine completion can (via TryApplyRaidedVillage's own quest-success side effects
        // once a later turn-in finalizes the quest) mutate IssueManager.Issues, and MBReadOnlyDictionary's own
        // enumerator doesn't tolerate that mid-iteration - same precaution as
        // NewIssueTypesAlternativeSolutionPatches.OnHourlyTick / TheConquestOfSettlementSiegeCompletionPatches.
        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (kvp.Value is not RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue) continue;
            if (kvp.Value.IssueQuest is not RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest quest) continue;

            // Try every attacking party in turn - Resolve itself rejects any that don't match this quest's real
            // recorded owner, so this is cheap (a raid's attacker side is rarely more than one or two parties)
            // and keeps Resolve's own signature simple (settlement + a single candidate party) - see its own
            // doc comment for why that matters for testability.
            foreach (var attackerMapEventParty in mapEvent.AttackerSide.Parties)
            {
                Resolve(quest, mapEvent.MapEventSettlement, attackerMapEventParty.Party?.MobileParty);
            }
        }
    }

    /// <summary>The actual bug fix/decision logic - resolves whether <paramref name="candidateAttackerParty"/> IS
    /// this quest's real recorded owner's own party, and if so applies+broadcasts the raided village. Internal
    /// (not private) and taking simple, directly-constructible primitives rather than a live
    /// <see cref="MapEvent"/>, specifically so tests can invoke it directly against an already-held quest
    /// reference without building a full MapEvent/MapEventSide object graph - same precedent as
    /// <c>TheConquestOfSettlementSiegeCompletionPatches.Resolve</c>'s own doc comment on why (this harness's
    /// <c>IObjectManager</c> does not return the same Hero instance across separate calls, so a fresh
    /// <c>IssueManager.Issues</c> scan can silently fail to reference-match a Hero the test already holds).</summary>
    internal static void Resolve(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest quest, Settlement raidedSettlement, MobileParty candidateAttackerParty)
    {
        if (quest == null || quest.IsFinalized) return;
        if (raidedSettlement == null || candidateAttackerParty == null) return;
        if (quest._raidedVillages.Contains(raidedSettlement)) return;

        if (!RaidAnEnemyTerritoryIssueOwnershipResolver.TryResolveOwner(quest.QuestGiver, out _, out var ownerParty))
        {
            // No recorded owner (e.g. a race between accept and this event, which should never happen in
            // practice since accept ownership recording is synchronous with the accept broadcast) - fail safe,
            // matching TheConquestOfSettlementSiegeCompletionPatches' own precedent.
            Logger.Warning("Could not resolve the real owner for a Raid an Enemy Territory issue given by {QuestGiver}; ignoring this raid completion", quest.QuestGiver);
            return;
        }
        if (!ReferenceEquals(candidateAttackerParty, ownerParty)) return;

        if (!ContainerProvider.TryResolve<IRaidAnEnemyTerritoryIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryApplyRaidedVillage(quest.QuestGiver, raidedSettlement)) return;

        MessageBroker.Instance.Publish(quest, new RaidAnEnemyTerritoryVillageRaided(quest.QuestGiver, raidedSettlement));
    }
}
