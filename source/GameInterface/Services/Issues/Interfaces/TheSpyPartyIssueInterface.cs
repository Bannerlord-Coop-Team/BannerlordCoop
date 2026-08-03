using Common.Util;
using HarmonyLib;
using SandBox.Issues;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.TheSpyPartyIssueCreationPatch"/>,
/// <see cref="Patches.TheSpyPartyIssueAcceptancePatch"/> and <see cref="Handlers.TheSpyPartyIssueHandler"/> need
/// to capture and authoritatively replicate a
/// <see cref="TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue"/>/<see cref="TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest"/>.
/// This type genuinely needs BOTH a bespoke creation capture AND a bespoke accept-time capture - unlike Nearby
/// Bandit Base/Prodigal Son (creation-only) or Village Needs Crafting Materials (accept-only):
///
/// 1. CREATION rolls <c>selectedSettlement</c> via <c>Extensions.GetRandomElementWithPredicate&lt;Settlement&gt;</c>
///    (a genuine dice roll among the issue owner's clan's towns) before the ctor - same shape as Captured by
///    Bounty Hunters'/Nearby Bandit Base's settlement capture.
///
/// 2. ACCEPT rolls <c>_selectedSpy</c> - <c>Extensions.GetRandomElement&lt;SuspectNpc&gt;(_suspectList)</c>,
///    called INSIDE the <c>TheSpyPartyIssueQuest</c> constructor itself (i.e. inside <c>GenerateIssueQuest</c>,
///    at accept time, not creation time). This is THE load-bearing roll for the whole quest - literally "who is
///    the spy" - so a bare, uncorrected replay of <c>IssueManager.StartIssueQuest</c> on another peer (the way
///    the fully generic mirror works for e.g. Nearby Bandit Base) would let each peer's own mirrored quest
///    object independently, differently decide who the real spy is. Captured once (as an INDEX into
///    <c>_suspectList</c>, not the raw <c>SuspectNpc</c>/<c>CharacterObject</c> - see <see cref="TryCaptureSelectedSpyIndex"/>'s
///    doc comment for why an index is the correct, environment-robust unit to broadcast here) right after the
///    genuine accept, and force-written onto every OTHER peer's own replicated quest object - same
///    capture-once/force-write shape as Village Needs Crafting Materials' per-client amount/reward, just for an
///    identity instead of a number.
///
/// Verified NOT needing capture (see the two <c>MBRandom.RandomFloat</c> rolls inside
/// <c>GetTownsPeopleDialogFlow</c>'s dialogue Condition/Consequence, which choose which flavor-text player-option
/// shows and whether a townsperson gives a clue this attempt): these run purely inside whichever peer's own LIVE
/// local conversation happens to be talking to a townsperson NPC. The clue-flags they gate
/// (<c>_playerLearnedHasHair</c> etc.) are read only by that SAME peer's own later dialogue conditions in that
/// SAME quest object - never broadcast, never compared against another peer's copy, and never used to determine
/// <c>_playerManagedToFindSpy</c> (that depends solely on the captured/forced <c>_selectedSpy</c> and the
/// player's own choice of who to duel). This is the exact same "dialogue Condition/Consequence roll never
/// crosses a peer boundary because each peer's own dialogue flow is wired once via its own local
/// <c>StartQuest</c>/replay and never touched again" reasoning already established for
/// <c>BettingFraudIssueBehavior</c>'s own dialogue-condition roll - confirmed here by checking THIS quest's own
/// <c>RegisterEvents</c> (only campaign-event listeners, no dialogue re-registration) and <c>SetDialogs</c>
/// (only ever called from the ctor/<c>InitializeQuestOnGameLoad</c>, both per-peer-local, never from a
/// network-triggered path) - so a hint learned on one peer's own local townsfolk conversation simply doesn't
/// exist on any other peer's own separate quest object copy; at worst a different peer re-hears the same clue,
/// which is harmless flavor, not a state divergence.
/// </summary>
public interface ITheSpyPartyIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue issue, out Settlement selectedSettlement);

    TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue ConstructReplicated(Hero owner, Settlement selectedSettlement);

    void RegisterReplicated(Hero owner, TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue issue);

    /// <summary>
    /// Bare, uncorrected replay of <see cref="IssueManager.StartIssueQuest"/>, used ONLY by the server while
    /// arbitrating a client's accept request - identical shape/reasoning to
    /// <see cref="IVillageNeedsCraftingMaterialsIssueInterface.ReplayQuestAccepted"/>: the server has no
    /// authoritative spy index to force-write yet, replaying this IS what produces one (the server's own roll
    /// is authoritative by definition), then the server reads it back via <see cref="TryCaptureSelectedSpyIndex"/>
    /// before broadcasting.
    /// </summary>
    void ReplayQuestAccepted(Hero owner);

    /// <summary>
    /// Reads back which entry of <paramref name="owner"/>'s current
    /// <see cref="TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest"/>'s own (freshly rebuilt, per-peer
    /// identical-by-construction) <c>_suspectList</c> matches its just-rolled <c>_selectedSpy</c>, as a plain
    /// index (0-3) rather than the raw <c>SuspectNpc</c>/<c>CharacterObject</c> reference. An index is the
    /// correct unit here specifically because <c>_suspectList</c>'s four <c>CharacterObject</c> templates
    /// themselves depend on <c>_currentDifficultySuffix</c> (derived from <c>IssueDifficultyMultiplier</c> - a
    /// genuinely per-client value, same accepted-cosmetic-divergence shape as every other type's difficulty
    /// multiplier), so broadcasting the raw <c>CharacterObject</c> id could hand a peer with a DIFFERENT local
    /// difficulty suffix a template that isn't even in their own rebuilt <c>_suspectList</c> at all. Broadcasting
    /// "the i-th archetype slot" instead is robust to that: every peer force-writes <c>_selectedSpy</c> to the
    /// matching entry of their OWN locally-rebuilt <c>_suspectList</c>, which is self-consistent with the NPCs
    /// THEY will locally spawn in the settlement (also keyed off their own <c>_currentDifficultySuffix</c> - see
    /// <c>OnMissionStarted</c>/<c>CreateXSpyLocationCharacter</c> in the decompiled source), so whichever peer
    /// eventually plays the duel gets an internally-consistent spy identity regardless of any difficulty-suffix
    /// divergence. Returns false if the hero has no quest of this type yet, or the index can't be resolved.
    /// </summary>
    bool TryCaptureSelectedSpyIndex(Hero owner, out int selectedSpyIndex);

    /// <summary>
    /// The central mechanism (see the type doc comment): ensures <paramref name="owner"/> has a real quest
    /// object (replaying <see cref="ReplayQuestAccepted"/> if it doesn't yet), then force-writes
    /// <c>_selectedSpy</c> to the <paramref name="selectedSpyIndex"/>-th entry of THIS peer's own (already
    /// rebuilt) <c>_suspectList</c>. Idempotent: safe to call again with the same index (a resend, or the
    /// server's own broadcast echoing back to itself), and safe on the machine whose own roll already happened
    /// to land on the same index (a same-value no-op). A no-op entirely if <paramref name="owner"/>'s issue
    /// isn't a <see cref="TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue"/>, or the index is out of range.
    /// </summary>
    void MirrorQuestAccepted(Hero owner, int selectedSpyIndex);

    /// <summary>
    /// Rolls a losing peer's own already-applied (optimistic) local acceptance back after the server tells it
    /// another peer won the same-issue accept race. Identical, fully generic logic to
    /// <see cref="VillageNeedsToolsIssueInterface.RejectAcceptance"/>, duplicated here rather than shared so
    /// this feature stays self-contained (same precedent as
    /// <see cref="IVillageNeedsCraftingMaterialsIssueInterface.RejectAcceptance"/>).
    /// </summary>
    void RejectAcceptance(Hero owner);
}

/// <inheritdoc cref="ITheSpyPartyIssueInterface"/>
public class TheSpyPartyIssueInterface : ITheSpyPartyIssueInterface
{
    private static readonly FieldInfo SelectedSettlementField =
        AccessTools.Field(typeof(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue), "_selectedSettlement");
    private static readonly FieldInfo SelectedSpyField =
        AccessTools.Field(typeof(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest), "_selectedSpy");
    private static readonly FieldInfo SuspectListField =
        AccessTools.Field(typeof(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest), "_suspectList");

    public bool TryCaptureFields(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue issue, out Settlement selectedSettlement)
    {
        selectedSettlement = null;
        if (issue == null) return false;

        selectedSettlement = (Settlement)SelectedSettlementField.GetValue(issue);
        return selectedSettlement != null;
    }

    public TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue ConstructReplicated(Hero owner, Settlement selectedSettlement)
    {
        // The real ctor already takes the settlement directly (no independent roll to override) - construct
        // normally.
        return new TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue(owner, selectedSettlement);
    }

    public void RegisterReplicated(Hero owner, TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue), IssueBase.IssueFrequency.Rare);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureSelectedSpyIndex(Hero owner, out int selectedSpyIndex)
    {
        selectedSpyIndex = -1;

        if (owner?.Issue?.IssueQuest is not TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest quest) return false;

        var suspectList = (MBList<TheSpyPartyIssueQuestBehavior.SuspectNpc>)SuspectListField.GetValue(quest);
        var selectedSpy = (TheSpyPartyIssueQuestBehavior.SuspectNpc)SelectedSpyField.GetValue(quest);
        if (suspectList == null) return false;

        selectedSpyIndex = suspectList.IndexOf(selectedSpy);
        return selectedSpyIndex >= 0;
    }

    public void MirrorQuestAccepted(Hero owner, int selectedSpyIndex)
    {
        if (owner?.Issue is not TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest quest) return;

            var suspectList = (MBList<TheSpyPartyIssueQuestBehavior.SuspectNpc>)SuspectListField.GetValue(quest);
            if (suspectList == null || selectedSpyIndex < 0 || selectedSpyIndex >= suspectList.Count) return;

            SelectedSpyField.SetValue(quest, suspectList[selectedSpyIndex]);
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
