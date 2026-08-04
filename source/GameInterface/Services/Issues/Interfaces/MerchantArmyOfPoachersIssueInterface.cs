using System.Linq;
using Common.Util;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection/replay access <see cref="Patches.MerchantArmyOfPoachersIssueCreationPatch"/>,
/// <see cref="Patches.MerchantArmyOfPoachersPartySpawnGatePatch"/>,
/// <see cref="Patches.MerchantArmyOfPoachersBattleStartApprovalPatches"/> and
/// <see cref="Handlers.MerchantArmyOfPoachersIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue"/>.
///
/// CREATION: <c>MerchantArmyOfPoachersIssue</c>'s ctor takes <c>(Hero, Village)</c> directly - the village is
/// picked, per-hero, inside <c>MerchantArmyOfPoachersIssueBehavior.ConditionsHold</c> (called from
/// <c>OnCheckForIssue</c>) via <c>Village.GetRandomElementWithPredicate</c> - a genuinely RNG-dependent pick
/// (unlike Caravan Ambush's deterministic <c>MinBy</c> distance pick), so a bare independent replay on every
/// peer would NOT converge on the same village. <see cref="TryCaptureQuestVillage"/>/<see cref="ConstructReplicated"/>
/// follow the exact same "capture the server's real pick, broadcast it, every client constructs directly from
/// it" shape <c>ICaravanAmbushIssueInterface</c> already established for its own target settlement - see
/// <see cref="Patches.MerchantArmyOfPoachersIssueCreationPatch"/>.
///
/// ACCEPT TIME: <c>GenerateIssueQuest(questId)</c> forwards <c>IssueOwner</c>/a fixed 20-day due time/
/// <c>_questVillage</c>/<c>IssueDifficultyMultiplier</c>/<c>RewardGold</c> - all pure functions of the now-
/// correctly-replicated Issue-level state above, so a bare replay of <c>IssueManager.StartIssueQuest</c> on
/// every peer lands on a byte-identical <c>MerchantArmyOfPoachersIssueQuest</c> - see
/// <c>MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue</c>'s entry in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>. No bespoke accept-time
/// capture/force-write is needed for the Quest's own scalar fields.
///
/// POACHERS-PARTY SPAWN (the task's own decisive, confirmed bug): vanilla creates <c>_poachersParty</c> lazily,
/// inside the <c>army_of_poachers_village</c> game-menu's <c>on_init</c> callback
/// (<c>army_of_poachers_village_on_init</c>) - i.e. whichever peer's own client first happens to open that
/// menu (itself gated on local night-time/<c>PlayerEncounter.IsPlayerWaiting</c> state) creates the
/// world-visible party. There is no gameplay reason for this - the party just sits parked, AI-disabled, from
/// the moment the quest starts. Fixed by moving the spawn call itself off the menu callback entirely, onto the
/// accept-time flow: <see cref="Patches.MerchantArmyOfPoachersPartySpawnGatePatch"/> triggers
/// <see cref="CreatePoachersPartyOnServer"/> as a POSTFIX on the real, unmodified
/// <c>QuestAcceptedConsequences()</c> (a live <c>OfferDialogFlow.Consequence</c>, Category B in
/// doc/GroupA_HostileMobilePartySync_Design_v3.md §5 - only ever runs on the genuine accepter's own machine),
/// gated the same way as every other party-spawn gate this session (block-and-forward-as-request on a
/// client-owner, allow on server/host-owner). Vanilla's own <c>army_of_poachers_village_on_init</c> null-check
/// (<c>if (Instance._poachersParty == null &amp;&amp; !Hero.MainHero.IsWounded) CreatePoachersParty();</c>) is
/// left completely untouched - it becomes permanently-false dead code once the party always already exists by
/// the time that menu could ever open, which is harmless and not worth a separate patch to delete.
///
/// <see cref="CreatePoachersPartyOnServer"/> is NOT a bare reflective invoke of vanilla's own private
/// <c>CreatePoachersParty()</c> (unlike <c>IGangLeaderNeedsWeaponsIssueInterface.CreateGuardsPartyOnServer</c>'s
/// "faithful reuse, not hand-reimplementation" precedent) - a real, load-bearing correctness issue rules that
/// out here, independent of multiplayer sync: vanilla's own body ends with
/// <c>EnterSettlementAction.ApplyForParty(this._poachersParty, Settlement.CurrentSettlement)</c>, which is only
/// ever safe because vanilla's OWN trigger point only ever runs while the accepter is physically standing
/// inside <c>_questVillage.Settlement</c> (see <c>HourlyTick</c>'s own gate:
/// <c>PlayerEncounter.EncounterSettlement == this._questVillage.Settlement</c>) - <c>EnterSettlementAction.ApplyForParty</c>
/// unconditionally dereferences <c>settlement.SettlementComponent</c> with NO null guard, so a null
/// <c>Settlement.CurrentSettlement</c> (the ordinary case on a dedicated server, or on ANY machine at the
/// moment of accept, before the player has traveled to the village) would crash. Since this fix deliberately
/// triggers creation at a different, earlier moment than vanilla intended, that invariant no longer holds -
/// <see cref="CreatePoachersPartyOnServer"/> substitutes the always-correct, always-non-null
/// <c>_questVillage.Settlement</c> in its place, which is exactly what vanilla's own call always evaluated to
/// in every genuine playthrough - a faithful substitution, not a behavioral departure. Everything else in the
/// method body is a direct, unmodified port of vanilla's own logic. The bandit-clan/culture pick
/// (<c>SettlementHelper.FindNearestHideoutToMobileParty(MobileParty.MainParty, ...)</c>) is the one other
/// accepter-context read in the real body; like <c>ICaravanAmbushIssueInterface.CreateReplicatedAccept</c>'s own
/// documented bandit-hideout pick, this is accepted as a low-impact, cosmetic-only (troop-culture-skin)
/// divergence when this runs on the server on behalf of a remote-client owner, rather than built out with an
/// extra captured/forwarded field - flagged explicitly here rather than silently dropped.
///
/// BATTLE START (matching <c>GangLeaderNeedsWeaponsBattleStartApprovalPatches</c>'s pattern, per the task's own
/// item 3): unlike Gang Leader Needs Weapons' inline lambda, <c>StartQuestBattle()</c> is itself a named,
/// directly Harmony-patchable <c>internal</c> method - <see cref="Patches.MerchantArmyOfPoachersBattleStartApprovalPatches"/>
/// gates it directly (ownership-block for a non-owner, block-and-forward-as-request for a remote-client owner,
/// unmodified pass-through for a host/server owner). <see cref="InvokeRealStartQuestBattle"/> is only ever safe
/// to call on the genuine owner's own machine (enforced by the caller, not this method).
/// <see cref="ForceIsReadyToBeFinalized"/> is parity-only bookkeeping for non-owner mirrors, matching
/// <c>IGangLeaderNeedsWeaponsIssueInterface.ForceCheckForBattleResult</c>'s own doc comment - <c>_isReadyToBeFinalized</c>
/// is only ever meaningfully read together with THIS machine's own local <c>PlayerEncounter.Current</c> inside
/// <c>army_of_poachers_village_on_init</c>, which (per design doc §5 Category A) never meaningfully runs for a
/// non-owner anyway.
/// </summary>
public interface IMerchantArmyOfPoachersIssueInterface : IGameAbstraction
{
    /// <summary>Reads the rolled/picked quest village off an already-constructed issue (direct field access -
    /// see TaleWorlds.CampaignSystem's whole-assembly Publicize in GameInterface.csproj). Returns false if the
    /// village is missing, which should never happen for a real instance.</summary>
    bool TryCaptureQuestVillage(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue issue, out Village questVillage);

    /// <summary>
    /// Builds a <see cref="MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue"/> via its real
    /// public ctor, which takes the village directly - no field-forcing/reflection needed (see the type doc
    /// comment). Does not register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue ConstructReplicated(Hero owner, Village questVillage);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling the village on) a new one - same technique as
    /// <see cref="CaravanAmbushIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue issue);

    /// <summary>Reads <c>_poachersParty</c> off <paramref name="owner"/>'s current
    /// <see cref="MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest"/>. Returns false if the
    /// hero has no quest of this type yet, or the party hasn't been spawned/mirrored on this peer yet.</summary>
    bool TryCapturePoachersParty(Hero owner, out MobileParty poachersParty);

    /// <summary>Force-writes <c>_poachersParty</c> onto <paramref name="owner"/>'s current quest, so every
    /// peer's own mirror references the SAME already-AutoRegistry-synced <see cref="MobileParty"/> the server
    /// genuinely created. A no-op if the owner has no
    /// <see cref="MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest"/> yet.</summary>
    void ForcePoachersParty(Hero owner, MobileParty poachersParty);

    /// <summary>
    /// Server-only: builds the real poachers <see cref="MobileParty"/> - a direct, faithful port of vanilla's
    /// own <c>CreatePoachersParty()</c> body (see the type doc comment for the one deliberate, documented
    /// substitution and the one accepted, documented divergence). Idempotent - a no-op returning the
    /// already-existing party if one is already set. Returns null if the owner has no active quest yet, or its
    /// quest village has no settlement (should never happen for a real instance).
    /// </summary>
    MobileParty CreatePoachersPartyOnServer(Hero owner);

    /// <summary>Parity-only bookkeeping (see the type doc comment) - force-sets <c>_isReadyToBeFinalized = true</c>
    /// on a NON-owner peer's own mirror once the server approves the genuine owner's battle start. A no-op if
    /// the owner has no active quest.</summary>
    void ForceIsReadyToBeFinalized(Hero owner);

    /// <summary>
    /// Invokes the REAL <c>StartQuestBattle()</c>, wrapped in <see cref="AllowedThread"/> so
    /// <see cref="Patches.MerchantArmyOfPoachersBattleStartApprovalPatches"/>'s own gate steps aside for this
    /// specific, already-approved call. Only ever safe/meaningful to call on the genuine owner's own machine -
    /// enforced by the caller (<see cref="Handlers.MerchantArmyOfPoachersIssueHandler"/>), not this method. A
    /// no-op if the owner has no active quest.
    /// </summary>
    void InvokeRealStartQuestBattle(Hero owner);
}

/// <inheritdoc cref="IMerchantArmyOfPoachersIssueInterface"/>
public class MerchantArmyOfPoachersIssueInterface : IMerchantArmyOfPoachersIssueInterface
{
    public bool TryCaptureQuestVillage(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue issue, out Village questVillage)
    {
        questVillage = null;
        if (issue == null) return false;

        questVillage = issue._questVillage;
        return questVillage != null;
    }

    public MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue ConstructReplicated(Hero owner, Village questVillage)
    {
        return new MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue(owner, questVillage);
    }

    public void RegisterReplicated(Hero owner, MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public bool TryCapturePoachersParty(Hero owner, out MobileParty poachersParty)
    {
        poachersParty = null;
        if (owner?.Issue?.IssueQuest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest quest) return false;

        poachersParty = quest._poachersParty;
        return poachersParty != null;
    }

    public void ForcePoachersParty(Hero owner, MobileParty poachersParty)
    {
        if (owner?.Issue?.IssueQuest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest quest) return;

        quest._poachersParty = poachersParty;
    }

    public MobileParty CreatePoachersPartyOnServer(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest quest) return null;
        if (quest._poachersParty != null) return quest._poachersParty;

        var questVillage = quest._questVillage;
        if (questVillage?.Settlement == null) return null;

        // Cosmetic-only, documented divergence when running on the server for a remote-client owner - see the
        // type doc comment (same shape/precedent as ICaravanAmbushIssueInterface.CreateReplicatedAccept's own
        // bandit-hideout pick).
        var closestHideout = SettlementHelper.FindNearestHideoutToMobileParty(MobileParty.MainParty, MobileParty.NavigationType.Default, x => x.IsActive);
        var clan = closestHideout != null
            ? Clan.BanditFactions.FirstOrDefault(t => t.Culture == closestHideout.Settlement.Culture)
            : Clan.BanditFactions.FirstOrDefault();

        var poachersParty = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
            questVillage.Settlement.GatePosition, 1f, null, new TextObject("{=WQa1R55u}Poachers Party"), clan,
            TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(), null, "", "", 0f, false);

        var leather = MBObjectManager.Instance.GetObject<ItemObject>("leather");
        var leatherCount = MathF.Ceiling(quest._difficultyMultiplier * 5f) + MBRandom.RandomInt(0, 2);
        poachersParty.ItemRoster.AddToCounts(leather, leatherCount * 2);

        var poacherTroop = CharacterObject.All.FirstOrDefault(t => t.StringId == "poacher");
        var troopCount = 10 + MathF.Ceiling(40f * quest._difficultyMultiplier);
        poachersParty.MemberRoster.AddToCounts(poacherTroop, troopCount, false, 0, 0, true, -1);

        poachersParty.SetPartyUsedByQuest(true);
        poachersParty.Ai.DisableAi();

        // The one deliberate, documented substitution - see the type doc comment: _questVillage.Settlement in
        // place of vanilla's own (only-ever-correct-at-menu-open-time) Settlement.CurrentSettlement.
        EnterSettlementAction.ApplyForParty(poachersParty, questVillage.Settlement);

        quest._poachersParty = poachersParty;
        return poachersParty;
    }

    public void ForceIsReadyToBeFinalized(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest quest) return;

        quest._isReadyToBeFinalized = true;
    }

    public void InvokeRealStartQuestBattle(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest quest) return;

        using (new AllowedThread())
        {
            quest.StartQuestBattle();
        }
    }
}
