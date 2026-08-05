using HarmonyLib;
using System.Reflection;
using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection/replay access <see cref="Patches.GangLeaderNeedsWeaponsIssueCreationPatch"/>,
/// <see cref="Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/>,
/// <see cref="Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches"/> and
/// <see cref="Handlers.GangLeaderNeedsWeaponsIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue"/>.
///
/// CREATION: <c>GangLeaderNeedsWeaponsIssue</c>'s ctor takes ONLY <c>(Hero issueOwner)</c> - confirmed by direct
/// decompile read (<c>E:\bannerlorddiffs\TaleWorlds.CampaignSystem\Issues\GangLeaderNeedsWeaponsIssueQuestBehavior.cs</c>),
/// not the design doc, which didn't cover creation for this type at all. The two fields it internally rolls
/// (<c>_requiredWeaponClassIndex</c>/<c>_averagePriceForItem</c>, via <c>CalculateAveragePriceForWeaponClass</c> +
/// <c>MBList.GetRandomElement()</c>) are BOTH fully deterministic given only shared, already-synchronized
/// campaign state: <c>_canBeRequestedWeaponClassList</c> has exactly ONE entry (<c>WeaponClass.OneHandedAxe</c>),
/// so <c>GetRandomElement()</c> over a single-element list is deterministic regardless of RNG state, and
/// <c>CalculateAveragePriceForWeaponClass</c> is a pure function of every town's <c>Settlement.ItemRoster</c> -
/// shared state every peer already has an identical copy of, not <c>MobileParty.MainParty</c>-relative. So
/// <see cref="ConstructReplicated"/> needs no captured/forwarded field at all beyond the owner - simpler than
/// every other bespoke interface in this family (even Smugglers/Caravan Ambush, whose Issue ctors take an
/// externally-picked settlement). This still needs its OWN creation-broadcast patch (unlike relying on
/// <c>IssueManagerCreateNewIssuePatches</c>'s own postfix, which is hardcoded to <c>VillageNeedsToolsIssue</c>
/// only) so every client constructs its own local mirror at all - the generic Prefix there already blocks ALL
/// client-originated <c>IssueManager.CreateNewIssue</c> calls unconditionally, so without a broadcast a client
/// would never see this NPC as having an issue to offer in the first place.
///
/// ACCEPT TIME: <c>GenerateIssueQuest(questId)</c> forwards <c>IssueOwner</c>/a fixed 25-day due time/
/// <c>RewardGold</c>/<c>_requiredWeaponClassIndex</c>/<c>RequestedWeaponAmount</c>/<c>IssueDifficultyMultiplier</c>/
/// <c>_averagePriceForItem</c> - all pure functions of the now-correctly-replicated Issue-level state above, so a
/// bare replay of <c>IssueManager.StartIssueQuest</c> on every peer lands on a byte-identical
/// <c>GangLeaderNeedsWeaponsIssueQuest</c> - see <c>GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue</c>'s
/// entry in <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>. No bespoke accept-time
/// capture/force-write is needed for this quest type's own scalar fields at all.
///
/// GUARDS-PARTY SPAWN (the standing-lesson nuance the task was seeded with, independently re-verified against
/// the decompiled source rather than trusted): unlike Smugglers/Caravan Ambush, <c>_guardsParty</c> is NOT set
/// by an accept-time dialogue Consequence - it's set by the private <c>CreateGuardsParty()</c>, called ONLY
/// from <c>OnSettlementEnter()</c>, itself wired via <c>RegisterEvents()</c>. Per the design doc's own §5
/// Category-A reasoning, <c>RegisterEvents()</c>-wired listeners are usually inert on a non-owner's mirror
/// because the STATE they depend on is never populated outside the accepter-only path - but that reasoning does
/// NOT hold here: <c>OnSettlementEnter</c>'s own gating condition (<c>party == MobileParty.MainParty</c>) is
/// trivially satisfiable by ANY peer's own local player walking into the SAME settlement (each peer has their
/// own local <c>MobileParty.MainParty</c>), and its job is precisely to POPULATE <c>_guardsParty</c> when still
/// null - the "state never populated outside the accepter" argument that protects e.g. CaravanAmbush's dialogue
/// listeners doesn't apply to the very listener that creates the state in the first place. Two independent bugs
/// follow from this, both fixed by <see cref="Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/>:
/// (1) a NON-owner peer's own settlement-entry could create (or crash trying to reference) a guards party that
/// belongs to someone else's quest; (2) if the genuine OWNER is a remote CLIENT, <c>CreateGuardsParty()</c>'s
/// call to <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c> hits the exact same
/// "CustomPartyComponent ctor hard-blocked on a client" bug Smugglers/Caravan Ambush both had (confirmed via
/// <see cref="GameInterface.Services.PartyComponents.Patches.Lifetime.CustomPartyComponentLifetimePatches"/>).
///
/// Unlike Smugglers/Caravan Ambush, <c>CreateGuardsParty()</c>'s ENTIRE body reads only already-shared,
/// deterministic state (<c>QuestGiver.CurrentSettlement</c>, its <c>OwnerClan</c>/<c>Culture</c>, and
/// <c>_issueDifficulty</c> - a frozen ctor parameter, identical on every peer's mirror) - it never reads
/// <c>MobileParty.MainParty</c> at all. So the server can independently, deterministically reproduce byte-for-
/// byte what the accepter's own blocked call would have produced, with ZERO captured/forwarded accepter-derived
/// value needed (a first among this family's party-spawn gates). <see cref="CreateGuardsPartyOnServer"/>
/// reflectively invokes the REAL private <c>CreateGuardsParty()</c> (not a hand-reimplementation, to avoid
/// subtly drifting from the troop-count/culture-guard-name logic) - safe/correct because the server is NEVER
/// blocked by <c>CustomPartyComponentLifetimePatches</c> (only <c>ModInformation.IsClient</c> is), and the
/// resulting <see cref="MobileParty"/> auto-replicates via the normal <c>GameInterface.Registry.Auto</c>
/// AutoRegistry machinery, same as every other genuine server-side party creation in this codebase.
///
/// The remaining wrinkle unique to this quest: <c>OnSettlementEnter()</c> doesn't just ENSURE the party exists -
/// in the SAME synchronous call it immediately opens a conversation with the guard leader
/// (<c>ConversationHelper.GetConversationCharacterPartyLeader(this._guardsParty.Party)</c>). On an owner-client
/// whose own call gets blocked-and-forwarded, <c>_guardsParty</c> is still null when the (skipped) vanilla body
/// would have tried to open that conversation - <see cref="ShouldTriggerGuardEncounter"/> replicates vanilla's
/// own top-level gating condition so the Patch can decide whether to forward a request at all, and
/// <see cref="OpenGuardConversationIfPossible"/> (called by
/// <see cref="Handlers.GangLeaderNeedsWeaponsIssueHandler"/> once the server's broadcast lands and force-writes
/// <c>_guardsParty</c> on the owner's own machine) finishes the side effect vanilla's own single synchronous
/// call couldn't, using the exact same public APIs (<see cref="ConversationCharacterData"/>/
/// <see cref="CampaignMapConversation"/>) vanilla itself uses - a documented, accepted, millisecond-scale timing
/// divergence (the guard encounter opens one network round-trip later than a genuine single-player host would
/// see), the same "network-latency tolerance" reasoning already established by
/// <c>ICaravanAmbushIssueInterface</c>'s own vicinity-check-timer doc comment.
///
/// BATTLE START (item 4 of the task, "BattleStartApprovalPatches"): <c>StartFight()</c>'s own mission-launch
/// calls (<c>PlayerEncounter.RestartPlayerEncounter</c>/<c>StartBattle</c>/
/// <c>CampaignMission.OpenBattleMissionWhileEnteringSettlement</c>) are LOCAL, UI/mission-launch APIs that only
/// ever make sense run on the genuine owner's own machine, whichever machine that is - unlike a party spawn,
/// there is no "the server does it instead" relocation available (the server running
/// <c>CampaignMission.OpenBattleMissionWhileEnteringSettlement</c> against ITS OWN, possibly-null-on-a-dedicated-
/// server <c>PartyBase.MainParty</c> would be nonsensical). What genuinely needs fixing instead: the dialogue
/// option that sets <c>_startBattleMission = true</c> is an inline, compiler-generated lambda Consequence with
/// no stable name to Harmony-patch directly (unlike CaravanAmbush's named Condition method), registered on
/// EVERY peer's own mirror (<c>SetDialogs()</c> runs unconditionally from the ctor) with no ownership check - so
/// without a gate, any connected peer who talks to the (now correctly globally-mirrored, per the guards-party
/// fix above) shared guardsParty leader could flip THEIR OWN local <c>_startBattleMission</c> flag and have
/// their own next "town" menu open independently trigger a bogus local mission-launch attempt against a battle
/// that isn't theirs - "whichever peer's menu happens to open first" racing ahead with no arbiter at all.
/// <see cref="InvokeRealStartFight"/> is only ever safe to call on the genuine owner's own machine (enforced by
/// the caller, not this method) - the server is the single arbiter of WHETHER the fight may start
/// (<see cref="Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches"/> gates the named, Harmony-patchable
/// <c>StartFight()</c> method itself - reached by the lambda's downstream <c>OnGameMenuOpened</c> poll,
/// catching the un-patchable lambda's effect indirectly), broadcasting one approval so every peer's mirror
/// converges instead of each independently racing its own local, unsynced flag.
/// <see cref="ForceCheckForBattleResult"/> is parity-only bookkeeping for non-owner mirrors - documented, not
/// load-bearing, since <c>_checkForBattleResult</c> is only ever meaningfully read against THIS machine's own
/// local <c>PlayerEncounter.Battle</c>, which a non-owner never legitimately has involving <c>_guardsParty</c>.
/// </summary>
public interface IGangLeaderNeedsWeaponsIssueInterface : IGameAbstraction
{
    /// <summary>Builds a <see cref="GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue"/> via
    /// its real public ctor, which takes only the owner (see the type doc comment - no reflection/capture
    /// needed). Does not register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.</summary>
    GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue ConstructReplicated(Hero owner);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling) a new one - same technique as
    /// <see cref="CaravanAmbushIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue issue);

    /// <summary>Reads <c>_guardsParty</c> off <paramref name="owner"/>'s current
    /// <see cref="GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest"/>. Returns false if
    /// the hero has no quest of this type yet, or the party hasn't been spawned/mirrored on this peer yet.</summary>
    bool TryCaptureGuardsParty(Hero owner, out MobileParty guardsParty);

    /// <summary>Force-writes <c>_guardsParty</c> (reflection - see the type doc comment) onto
    /// <paramref name="owner"/>'s current quest, so every peer's own mirror references the SAME already-
    /// AutoRegistry-synced <see cref="MobileParty"/> the server genuinely created. A no-op if the owner has no
    /// <see cref="GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest"/> yet.</summary>
    void ForceGuardsParty(Hero owner, MobileParty guardsParty);

    /// <summary>
    /// Server-only: reflectively invokes the REAL private <c>CreateGuardsParty()</c> (a faithful reuse of
    /// vanilla's own logic rather than a hand-reimplementation - see the type doc comment for why no
    /// accepter-derived capture is needed). Idempotent - a no-op returning the already-existing party if one is
    /// already set. Returns null if the owner has no active quest yet, or its quest giver has no current
    /// settlement (should never happen for a real instance).
    /// </summary>
    MobileParty CreateGuardsPartyOnServer(Hero owner);

    /// <summary>
    /// Replicates <c>OnSettlementEnter</c>'s own top-level gating condition (reflection-read of its private
    /// fields) - used by <see cref="Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/> to decide whether
    /// an owner-client's settlement-entry is even worth forwarding a spawn request for, matching vanilla's own
    /// gate faithfully instead of forwarding unconditionally.
    /// </summary>
    bool ShouldTriggerGuardEncounter(
        GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest, MobileParty party, Settlement settlement);

    /// <summary>
    /// Opens the guard conversation using the same public APIs vanilla's own <c>OnSettlementEnter</c> body uses
    /// - see the type doc comment for why this exists as its own callable step (the owner-client's blocked call
    /// couldn't finish this synchronously). A no-op if the quest has no guards party yet.
    /// </summary>
    void OpenGuardConversationIfPossible(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest);

    /// <summary>Parity-only bookkeeping (see the type doc comment) - force-sets <c>_checkForBattleResult = true</c>
    /// on a NON-owner peer's own mirror once the server approves the genuine owner's battle start. A no-op if
    /// the owner has no active quest.</summary>
    void ForceCheckForBattleResult(Hero owner);

    /// <summary>
    /// Reflectively invokes the REAL private <c>StartFight()</c>, wrapped in <see cref="AllowedThread"/> so
    /// <see cref="Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches"/>'s own gate steps aside for this
    /// specific, already-approved call. Only ever safe/meaningful to call on the genuine owner's own machine -
    /// enforced by the caller (<see cref="Handlers.GangLeaderNeedsWeaponsIssueHandler"/>), not this method. A
    /// no-op if the owner has no active quest.
    /// </summary>
    void InvokeRealStartFight(Hero owner);
}

/// <inheritdoc cref="IGangLeaderNeedsWeaponsIssueInterface"/>
public class GangLeaderNeedsWeaponsIssueInterface : IGangLeaderNeedsWeaponsIssueInterface
{
    private static readonly FieldInfo GuardsPartyField =
        AccessTools.Field(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "_guardsParty");
    private static readonly FieldInfo PlayerDodgedGuardsField =
        AccessTools.Field(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "_playerDodgedGuards");
    private static readonly FieldInfo PlayerGoBackField =
        AccessTools.Field(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "_playerGoBack");
    private static readonly FieldInfo RequestedWeaponAmountField =
        AccessTools.Field(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "_requestedWeaponAmount");
    private static readonly FieldInfo CollectedItemAmountField =
        AccessTools.Field(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "_collectedItemAmount");
    private static readonly FieldInfo CheckForBattleResultField =
        AccessTools.Field(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "_checkForBattleResult");

    private static readonly MethodInfo CalculateAndSetRequestedItemCountOnPlayerMethod =
        AccessTools.Method(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "CalculateAndSetRequestedItemCountOnPlayer");
    private static readonly MethodInfo CreateGuardsPartyMethod =
        AccessTools.Method(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "CreateGuardsParty");
    private static readonly MethodInfo StartFightMethod =
        AccessTools.Method(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest), "StartFight");

    public GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue ConstructReplicated(Hero owner)
    {
        return new GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue(owner);
    }

    public void RegisterReplicated(Hero owner, GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public bool TryCaptureGuardsParty(Hero owner, out MobileParty guardsParty)
    {
        guardsParty = null;
        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest) return false;

        guardsParty = (MobileParty)GuardsPartyField.GetValue(quest);
        return guardsParty != null;
    }

    public void ForceGuardsParty(Hero owner, MobileParty guardsParty)
    {
        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest) return;

        GuardsPartyField.SetValue(quest, guardsParty);
    }

    public MobileParty CreateGuardsPartyOnServer(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest) return null;

        if (GuardsPartyField.GetValue(quest) is MobileParty existing && existing != null) return existing;
        if (quest.QuestGiver?.CurrentSettlement == null) return null;

        // Deliberately NOT wrapped in AllowedThread - this must look like a genuine, novel server-side creation
        // (same as the host's own real accept path already does, unwrapped) so
        // CustomPartyComponentLifetimePatches / GameInterface.Registry.Auto's MobileParty AutoRegistry both take
        // their real "server created this for the first time" branch - see SmugglersIssueInterface's own doc
        // comment for the precedent this follows.
        CreateGuardsPartyMethod.Invoke(quest, null);

        return (MobileParty)GuardsPartyField.GetValue(quest);
    }

    public bool ShouldTriggerGuardEncounter(
        GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest, MobileParty party, Settlement settlement)
    {
        if (quest?.QuestGiver == null) return false;
        if ((bool)PlayerDodgedGuardsField.GetValue(quest)) return false;
        if (party != MobileParty.MainParty) return false;
        if (settlement != quest.QuestGiver.CurrentSettlement) return false;
        if (MobileParty.MainParty?.Army != null) return false;
        if (Campaign.Current.GameMenuManager?.NextLocation != null) return false;
        // GameStateManager.Current is null in this project's own lightweight campaign-only test harness (never
        // stood up - the same "out of scope" state CaravanAmbush/Smugglers' own scope notes already establish
        // for their bandit-hideout/caravan-template pipelines) - defensively treated the same as "not a live
        // MapState" rather than throwing, matching vanilla's own practical behavior whenever no map screen is
        // active at all.
        if (GameStateManager.Current?.ActiveState is not TaleWorlds.CampaignSystem.GameState.MapState) return false;
        if (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.EncounterSettlement == null) return false;

        CalculateAndSetRequestedItemCountOnPlayerMethod.Invoke(quest, null);

        var collectedItemAmount = (int)CollectedItemAmountField.GetValue(quest);
        var requestedWeaponAmount = (int)RequestedWeaponAmountField.GetValue(quest);
        var playerGoBack = (bool)PlayerGoBackField.GetValue(quest);

        return collectedItemAmount >= requestedWeaponAmount / 3 && !playerGoBack;
    }

    public void OpenGuardConversationIfPossible(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest)
    {
        if (quest == null) return;
        var guardsParty = (MobileParty)GuardsPartyField.GetValue(quest);
        if (guardsParty == null) return;

        var conversationPartnerData = new ConversationCharacterData(
            TaleWorlds.CampaignSystem.Conversation.ConversationHelper.GetConversationCharacterPartyLeader(guardsParty.Party),
            null, false, false, false, true, false, false);
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, false, false, false, false, false, false),
            conversationPartnerData);
    }

    public void ForceCheckForBattleResult(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest) return;

        CheckForBattleResultField.SetValue(quest, true);
    }

    public void InvokeRealStartFight(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest quest) return;

        using (new AllowedThread())
        {
            StartFightMethod.Invoke(quest, null);
        }
    }
}
