using Common.Util;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection/publicized-field access <see cref="Patches.IssueManagerCreateNewIssuePatches"/> and
/// <see cref="Handlers.VillageNeedsToolsIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/> without letting each client's own
/// constructor call re-roll its terms.
///
/// <see cref="MirrorQuestAccepted"/> relies on a further determinism guarantee, worth spelling out since it's
/// the crux of Finding 1's fix: <c>VillageNeedsToolsIssue.GenerateIssueQuest</c> (the decompiled source) only
/// ever reads <c>_requestedItem</c>/<c>_numberOfRequestedItem</c>/<c>_exchangeItem</c>/<c>_numberOfExchangeItem</c>/
/// <c>_payment</c> (all already captured and broadcast byte-identical at creation time by
/// <see cref="Patches.IssueManagerCreateNewIssuePatches"/>/<see cref="ConstructReplicated"/>) plus a
/// deterministic 30-day <c>CampaignTime</c> and the issue's own <c>StringId</c> (itself already
/// byte-identical everywhere - every peer's <see cref="IssueManager"/> only ever calls
/// <c>CreateNewIssue</c> for this issue type in the same broadcast order, so its internal
/// <c>_nextIssueUniqueIndex</c> counter stays in lockstep). None of that is randomness, and none of it is
/// hero/player-specific - so simply replaying the real <see cref="IssueManager.StartIssueQuest"/> under
/// <see cref="AllowedThread"/> on every OTHER peer (see <see cref="Patches.IssueQuestAcceptancePatch"/>)
/// reconstructs a byte-identical <c>VillageNeedsToolsIssueQuest</c> without needing to separately
/// capture/broadcast its own fields. The one field <c>StartIssueWithQuest</c> also touches,
/// <c>_issueDifficultyMultiplier</c> (re-rolled from <c>Campaign.Current.PlayerProgress</c>, which - unlike
/// everything above - genuinely can differ per peer), is never read again anywhere in the quest-solution
/// path (only the alternative-solution duration/casualty rolls read it, and that path is deliberately not
/// replayed this way - see <see cref="MirrorAlternativeAccepted"/>), so a harmless, purely-cosmetic
/// divergence in that one saved-but-unread field is an accepted trade-off, not a real desync.
/// </summary>
public interface IVillageNeedsToolsIssueInterface : IGameAbstraction
{
    /// <summary>
    /// Reads the four rolled fields off an already-constructed issue (direct field access - see
    /// TaleWorlds.CampaignSystem's whole-assembly Publicize in GameInterface.csproj). Returns false if the
    /// issue has no requested item, which should never happen for a real instance.
    /// </summary>
    bool TryCaptureFields(
        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue,
        out ItemObject requestedItem,
        out ItemObject exchangeItem,
        out int numberOfExchangeItem,
        out int numberOfRequestedItem,
        out int payment);

    /// <summary>
    /// Builds a <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/> via its normal
    /// constructor, then overwrites its four derived (and otherwise client-divergent) readonly fields with
    /// the server's authoritative values via reflection. Does not register it with the
    /// <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue ConstructReplicated(
        Hero owner,
        ItemObject requestedItem,
        ItemObject exchangeItem,
        int numberOfExchangeItem,
        int numberOfRequestedItem,
        int payment);

    /// <summary>
    /// Registers an already-built, already-forced issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping (StringId
    /// assignment, <c>AfterCreation</c>, dictionary add, <c>Hero.OnIssueCreatedForHero</c>, tracked-object
    /// registration, the <c>OnNewIssueCreated</c> event) via a custom <see cref="PotentialIssueData"/> whose
    /// <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of constructing (and re-rolling) a
    /// new one.
    /// </summary>
    void RegisterReplicated(Hero owner, VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue);

    /// <summary>
    /// Mirrors a received issue-removal broadcast (or replays a locally-requested one on the server) by
    /// calling the correct <c>QuestBase.CompleteQuestWithXxx</c>/<c>IssueBase.CompleteIssueWithXxx</c>/bare
    /// <c>IssueFinalized</c> for <paramref name="reason"/> (see <see cref="VillageIssueFinalizeReason"/>),
    /// under an <see cref="AllowedThread"/> scope. A no-op if the hero has no issue (already finalized
    /// locally, or a stale/duplicate message).
    /// </summary>
    void FinalizeMirror(Hero owner, VillageIssueFinalizeReason reason);

    /// <summary>
    /// Server-authoritative (or mirror-peer) replay of <see cref="IssueManager.StartIssueQuest"/> for a hero
    /// whose issue is still <c>IsOngoingWithoutQuest</c>, run under <see cref="AllowedThread"/>. See the type
    /// doc comment for why this is safe/deterministic - every peer ends up with a byte-identical
    /// <c>VillageNeedsToolsIssueQuest</c> without needing to separately capture/broadcast its own fields. A
    /// no-op if the issue is no longer <c>IsOngoingWithoutQuest</c> (already applied - keeps this idempotent
    /// against a resend or a race where the local, genuine accept already ran).
    /// </summary>
    void MirrorQuestAccepted(Hero owner);

    /// <summary>
    /// Forces just the parent Issue's state to <c>SolvingWithAlternativeSolution</c> on a peer OTHER than the
    /// one that actually accepted (reflection, mirroring this type's existing forced-field convention) -
    /// deliberately NOT a full replay of <c>IssueBase.StartIssueWithAlternativeSolution</c>, since that
    /// method's <c>AlternativeSolutionSentTroops</c>/<c>AlternativeSolutionHero</c> are genuinely local to
    /// whichever player ran the party-screen troop-selection flow (see
    /// <c>Patches.IssueManagerTickPatches</c>'s documented, accepted "no ownership model for who accepted the
    /// alternative solution" limitation - the same gap, one layer up). This just stops
    /// <c>IsOngoingWithoutQuest</c> from staying true forever on every OTHER peer, which is Finding 1's actual
    /// concrete bug (a second player independently accepting the same issue). A no-op if the issue is no
    /// longer <c>IsOngoingWithoutQuest</c>.
    /// </summary>
    void MirrorAlternativeAccepted(Hero owner);

    /// <summary>
    /// Rolls a losing peer's own already-applied (optimistic) local acceptance back after the server tells it
    /// another peer won the same-issue accept race - see <see cref="Handlers.VillageNeedsToolsIssueHandler"/>.
    /// Reuses <c>IssueBase.CompleteIssueWithCancel()</c>, which already correctly branches for both an
    /// in-progress quest and an in-progress alternative solution (returning any committed troops via
    /// <c>IssueManager.TryToMakeTroopsReturn</c>) - the same outcome vanilla itself gives any other
    /// legitimately-cancelled issue (e.g. a war declaration), so this is a vanilla-consistent rollback rather
    /// than a bespoke one. A no-op if the hero's issue is still <c>IsOngoingWithoutQuest</c> (nothing to roll
    /// back - we never actually applied anything locally, e.g. a duplicate reject).
    /// </summary>
    void RejectAcceptance(Hero owner);
}

/// <inheritdoc cref="IVillageNeedsToolsIssueInterface"/>
public class VillageNeedsToolsIssueInterface : IVillageNeedsToolsIssueInterface
{
    private static readonly FieldInfo ExchangeItemField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_exchangeItem");
    private static readonly FieldInfo NumberOfExchangeItemField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_numberOfExchangeItem");
    private static readonly FieldInfo NumberOfRequestedItemField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_numberOfRequestedItem");
    private static readonly FieldInfo PaymentField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_payment");

    // IssueBase.IssueState is a private nested enum - reflected once, statically, rather than per-call.
    private static readonly Type IssueStateEnumType =
        AccessTools.Inner(typeof(IssueBase), "IssueState");
    private static readonly FieldInfo IssueStateField =
        AccessTools.Field(typeof(IssueBase), "_issueState");
    private static readonly object SolvingWithAlternativeSolutionStateValue =
        Enum.Parse(IssueStateEnumType, "SolvingWithAlternativeSolution");
    private static readonly PropertyInfo IsTriedToSolveBeforeProperty =
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.IsTriedToSolveBefore));

    public bool TryCaptureFields(
        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue,
        out ItemObject requestedItem,
        out ItemObject exchangeItem,
        out int numberOfExchangeItem,
        out int numberOfRequestedItem,
        out int payment)
    {
        requestedItem = null;
        exchangeItem = null;
        numberOfExchangeItem = 0;
        numberOfRequestedItem = 0;
        payment = 0;
        if (issue == null) return false;

        requestedItem = issue._requestedItem;
        exchangeItem = issue._exchangeItem;
        numberOfExchangeItem = issue._numberOfExchangeItem;
        numberOfRequestedItem = issue._numberOfRequestedItem;
        payment = issue._payment;

        return requestedItem != null;
    }

    public VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue ConstructReplicated(
        Hero owner,
        ItemObject requestedItem,
        ItemObject exchangeItem,
        int numberOfExchangeItem,
        int numberOfRequestedItem,
        int payment)
    {
        // The public ctor is the only way to build one, and it independently re-derives these same four
        // fields from IssueDifficultyMultiplier (Campaign.Current.PlayerProgress - a per-client value) and
        // the village's live Hearth. Build it the normal way for everything else it sets up (SaveableField
        // wiring, base IssueBase state), then force these four `readonly` fields (hence the reflection) to
        // the server's authoritative rolls so every client ends up with byte-identical quest terms.
        var issue = new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(owner, requestedItem);

        ExchangeItemField.SetValue(issue, exchangeItem);
        NumberOfExchangeItemField.SetValue(issue, numberOfExchangeItem);
        NumberOfRequestedItemField.SetValue(issue, numberOfRequestedItem);
        PaymentField.SetValue(issue, payment);

        return issue;
    }

    public void RegisterReplicated(Hero owner, VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void FinalizeMirror(Hero owner, VillageIssueFinalizeReason reason)
    {
        if (owner?.Issue == null) return;

        using (new AllowedThread())
        {
            var quest = owner.Issue.IssueQuest;
            if (quest != null && quest.IsOngoing)
            {
                switch (reason)
                {
                    case VillageIssueFinalizeReason.QuestSuccess:
                        quest.CompleteQuestWithSuccess();
                        return;
                    case VillageIssueFinalizeReason.QuestCancel:
                        quest.CompleteQuestWithCancel();
                        return;
                    case VillageIssueFinalizeReason.QuestFail:
                        quest.CompleteQuestWithFail();
                        return;
                    case VillageIssueFinalizeReason.QuestTimeout:
                        quest.CompleteQuestWithTimeOut();
                        return;
                    case VillageIssueFinalizeReason.QuestBetrayal:
                        quest.CompleteQuestWithBetrayal();
                        return;
                    default:
                        // An active quest exists but the reason didn't say how it ended (IssueOnly/
                        // RejectedAccept shouldn't happen alongside a real quest, but if they ever do, fail
                        // safe to a cancel instead of orphaning the quest via a bare IssueFinalized()).
                        quest.CompleteQuestWithCancel();
                        return;
                }
            }

            if (reason == VillageIssueFinalizeReason.RejectedAccept)
            {
                // No quest object to cascade through (e.g. a rejected alternative-solution accept) - reuse
                // the vanilla cancel path, which also correctly returns any committed alt-solution troops.
                owner.Issue.CompleteIssueWithCancel();
                return;
            }

            owner.Issue.IssueFinalized();
        }
    }

    public void MirrorQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public void MirrorAlternativeAccepted(Hero owner)
    {
        if (owner?.Issue is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue || !issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            IssueStateField.SetValue(issue, SolvingWithAlternativeSolutionStateValue);
            IsTriedToSolveBeforeProperty.SetValue(issue, true);
            issue.IssueDueTime = CampaignTime.Never;
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }
}
