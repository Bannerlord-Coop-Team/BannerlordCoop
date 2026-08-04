using Common.Util;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the access <see cref="Patches.LandLordCompanyOfTroubleIssueCreationPatch"/>,
/// <see cref="Patches.LandLordCompanyOfTroubleEnemyPartySpawnGatePatch"/> and
/// <see cref="Handlers.LandLordCompanyOfTroubleIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue"/>.
///
/// CREATION: <c>LandLordCompanyOfTroubleIssue</c>'s ctor takes ONLY <c>(Hero issueOwner)</c> - confirmed by
/// direct decompile read. Nothing is rolled at creation time; <c>CompanyTroopCount</c> (read later, at accept
/// time, by <c>GenerateIssueQuest</c>) is a pure function of <c>IssueDifficultyMultiplier</c>, the same
/// deterministic, shared, <c>Campaign.Current.PlayerProgress</c>-derived value every other type in this family
/// already relies on - so <see cref="ConstructReplicated"/> needs no captured/forwarded field at all, the
/// simplest creation shape in this family (same as <c>GangLeaderNeedsWeaponsIssue</c>). This still needs its OWN
/// creation-broadcast patch (unlike relying on <c>IssueManagerCreateNewIssuePatches</c>'s own postfix, which is
/// hardcoded to <c>VillageNeedsToolsIssue</c> only) so every client constructs its own local mirror at all.
///
/// ACCEPT TIME: <c>GenerateIssueQuest(questId)</c> forwards <c>IssueOwner</c>/<c>CampaignTime.Never</c>/
/// <c>CompanyTroopCount</c> - all pure functions of the now-correctly-replicated Issue-level state, so a bare
/// replay of <c>IssueManager.StartIssueQuest</c> on every peer lands on a byte-identical
/// <c>LandLordCompanyOfTroubleIssueQuest</c> - see this type's entry in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>. The Quest's OWN ctor then embeds
/// <c>_companyTroopCount</c> copies of the shared <c>"company_of_trouble_character"</c> troop template directly
/// into <c>MobileParty.MainParty.MemberRoster</c> - but that happens later, inside the dialogue Consequence
/// <c>QuestAcceptedConsequences()</c> (Category B - only ever runs on the genuine accepter's own machine), not
/// as part of this replay, so no bespoke accept-time capture is needed for that either.
///
/// THE CORE STRUCTURAL DIFFERENCE FROM EVERY OTHER GROUP A TYPE (TRIGGER-TIME, NOT CREATION-TIME, CAPTURE): for
/// most of this quest's life the "mercs" are just troops sitting directly in the OWNER'S OWN
/// <c>MobileParty.MainParty.MemberRoster</c> - not a separate <see cref="MobileParty"/> at all. Only when the
/// hostile-turn dialogue branch ("So be it!") fires does <c>CreateCompanyEnemyParty()</c> spin up a real
/// <c>BanditPartyComponent.CreateBanditParty(...)</c> party - and BOTH its spawn position
/// (<c>MobileParty.MainParty.Position</c>) AND its troop count (<c>_companyTroopCount</c>, last refreshed by
/// vanilla's own <c>UpdateCompanyTroopCount()</c>) are read from the OWNER'S OWN live state AT THAT EXACT
/// MOMENT - not at accept time, and not something any earlier accept-time capture could have frozen, since the
/// owner's roster/position at accept time (when they still have zero embedded troops and haven't necessarily
/// moved anywhere) has nothing to do with their roster/position hours or days later when the hostile turn
/// actually fires. <c>CreateCompanyEnemyParty()</c> itself is Category B (only ever reached via
/// <c>Campaign.Current.ConversationManager.ConversationEndOneShot</c>, itself only ever subscribed from the
/// "So be it!" player option inside <c>GetCompanyDialogFlow()</c>, itself only ever opened by
/// <c>company_of_trouble_menu_on_init</c>'s <c>_triggerCompanyOfTroubleConversation</c> branch, itself only ever
/// set true by this quest's own <c>HourlyTick()</c> - and <c>HourlyTick</c>'s own gating condition
/// (<c>MobileParty.MainParty.MemberRoster.TotalManCount - _companyTroopCount &lt;= _companyTroopCount</c>) is
/// self-limiting to the genuine owner: on any OTHER peer's own mirror, <c>_companyTroopCount</c> is always 0
/// (their own MainParty never received the embedded-troop injection, since <c>QuestAcceptedConsequences()</c>
/// never ran on their machine either), so the condition can never trigger there). Independently re-verified,
/// confirming the task's own "trigger-time capture-once-force-write" framing exactly.
///
/// <see cref="Patches.LandLordCompanyOfTroubleEnemyPartySpawnGatePatch"/> follows the SAME "let the real body run
/// unmodified on a host-owner, block-and-forward on a client-owner" shape <c>CaravanAmbushPartySpawnGatePatch</c>
/// established: on a host-owner, vanilla's own <c>CreateCompanyEnemyParty()</c> runs completely unmodified (its
/// own <c>MobileParty.MainParty</c> reads are already correct, since the host IS the accepter), and a Postfix
/// captures+broadcasts the resulting <c>_companyOfTroubleParty</c>. On a client-owner, <c>BanditPartyComponent</c>'s
/// ctor is hard-blocked (<see cref="GameInterface.Services.PartyComponents.BanditPartyComponents.Lifetime.BanditPartyComponentLifetimePatches"/>),
/// so the Prefix instead: (1) refreshes+captures <c>_companyTroopCount</c> and <c>MobileParty.MainParty.Position</c>
/// - see <see cref="RefreshCompanyTroopCount"/>, a faithful reimplementation of vanilla's own private
/// <c>UpdateCompanyTroopCount()</c> rather than a reflective invoke, since its body is a trivial 3-line roster
/// scan; (2) runs the one real-body line that's 100% safe to run locally regardless of client/host (removing the
/// embedded troops from the OWNER'S OWN already-existing <c>MainParty.MemberRoster</c> - not a blocked
/// PartyComponent construction); (3) forwards the captured position+count to the server instead of constructing
/// the party locally.
///
/// TASK ITEM 4 ("battle-start-approval mechanism, matching the prior 2 scripted-battle Group A types"): unlike
/// <c>GangLeaderNeedsWeaponsIssueQuestBehavior.StartFight()</c>/<c>MerchantArmyOfPoachersIssueBehavior.StartQuestBattle()</c>,
/// this quest has NO separately-callable, named battle-start method to gate independently - the actual mission
/// launch (<c>PlayerEncounter.Start()</c>/<c>OpenBattleMission</c>) is inline inside the STATIC
/// <c>company_of_trouble_menu_on_init</c>'s <c>_battleWillStart</c> branch, itself only ever reached on the true
/// owner's own machine (Category A - independently re-verified: <c>_battleWillStart</c> is only ever set true by
/// this SAME <c>CreateCompanyEnemyParty()</c> trigger, on the true owner's own local Quest mirror). Rather than
/// building a second, redundant round trip for a state transition with no independent trigger to gate, this
/// design FOLDS the battle-start-approval concept into the SAME trigger-capture round trip: <c>_battleWillStart</c>
/// only ever becomes true on ANY given machine (host OR client, owner OR bystander) AFTER
/// <see cref="NetworkLandLordCompanyOfTroubleEnemyPartySpawned"/> arrives - i.e. only after the server has
/// genuinely approved/created the party - satisfying the same "the local mission-launch can only ever happen
/// once the server says so" guarantee the other two quests' separate approval round trip provides, without a
/// second network round trip for a mechanism that (per the independent re-verification above) has no actual
/// non-owner race to close here.
///
/// <see cref="CreateCompanyEnemyPartyOnServer"/> is a faithful, parameterized reimplementation of the REST of
/// <c>CreateCompanyEnemyParty()</c>'s body (everything after the roster-removal, which already ran locally on
/// the accepter before this was ever forwarded) - using the captured <paramref name="position"/>/
/// <paramref name="troopCount"/> in place of the real method's own (server-side-wrong-whenever-the-genuine-owner
/// is a remote client) <c>MobileParty.MainParty</c> reads. The bandit-hideout pick
/// (<c>SettlementHelper.FindRandomSettlement(x =&gt; x.IsHideout)</c>) is the one other context read in the real
/// body - accepted as a low-impact, server's-own-local-context divergence (same documented precedent as
/// <c>ICaravanAmbushIssueInterface</c>'s own hideout pick), since a random hideout pick has no accepter-specific
/// "correct" answer to capture in the first place.
/// </summary>
public interface ILandLordCompanyOfTroubleIssueInterface : IGameAbstraction
{
    /// <summary>Builds a <see cref="LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue"/> via
    /// its real public ctor, which takes only the owner (see the type doc comment - no reflection/capture
    /// needed). Does not register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.</summary>
    LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue ConstructReplicated(Hero owner);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling) a new one - same technique as every other bespoke interface in this family.
    /// </summary>
    void RegisterReplicated(Hero owner, LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue issue);

    /// <summary>
    /// Faithful reimplementation of vanilla's own private <c>UpdateCompanyTroopCount()</c> (a trivial 3-line
    /// roster scan for <c>"company_of_trouble_character"</c>-type troops in <c>MobileParty.MainParty.MemberRoster</c>)
    /// - refreshes and returns <paramref name="owner"/>'s current quest's <c>_companyTroopCount</c>, called on
    /// THIS machine, so it must only ever be called on the genuine owner's own machine (enforced by the caller).
    /// Returns 0 if the owner has no <see cref="LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest"/>
    /// yet.
    /// </summary>
    int RefreshCompanyTroopCount(Hero owner);

    /// <summary>Reads <c>_companyOfTroubleParty</c> off <paramref name="owner"/>'s current quest. Returns false
    /// if the hero has no quest of this type yet, or the party hasn't been spawned/mirrored on this peer yet.</summary>
    bool TryCaptureCompanyEnemyParty(Hero owner, out MobileParty companyOfTroubleParty);

    /// <summary>Force-writes <c>_companyOfTroubleParty</c> AND <c>_battleWillStart = true</c> (see the type doc
    /// comment for why the latter is folded into this same force-write) onto <paramref name="owner"/>'s current
    /// quest, so every peer's own mirror references the SAME already-AutoRegistry-synced <see cref="MobileParty"/>
    /// the server genuinely created. A no-op if the owner has no
    /// <see cref="LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest"/> yet.</summary>
    void ForceCompanyEnemyParty(Hero owner, MobileParty companyOfTroubleParty);

    /// <summary>
    /// Server-only: a faithful, parameterized reimplementation of the REST of the real, private
    /// <c>CreateCompanyEnemyParty()</c>'s body (see the type doc comment for the full derivation) - builds the
    /// real hostile <see cref="MobileParty"/> using <paramref name="position"/>/<paramref name="troopCount"/>
    /// (captured from the genuine accepter's own machine) instead of THIS machine's own (possibly wrong)
    /// <c>MobileParty.MainParty</c>. Also force-writes the result via <see cref="ForceCompanyEnemyParty"/> before
    /// returning. Idempotent - a no-op returning the already-existing party if one is already set. Returns null
    /// if the owner has no active quest yet, or no hideout settlement exists to nominally attach the party to
    /// (should never happen for a real instance with real game data loaded).
    /// </summary>
    MobileParty CreateCompanyEnemyPartyOnServer(Hero owner, CampaignVec2 position, int troopCount);
}

/// <inheritdoc cref="ILandLordCompanyOfTroubleIssueInterface"/>
public class LandLordCompanyOfTroubleIssueInterface : ILandLordCompanyOfTroubleIssueInterface
{
    public LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue ConstructReplicated(Hero owner)
    {
        return new LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue(owner);
    }

    public void RegisterReplicated(Hero owner, LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue), IssueBase.IssueFrequency.Rare);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public int RefreshCompanyTroopCount(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest quest) return 0;

        var troopCount = 0;
        foreach (var item in MobileParty.MainParty.MemberRoster.GetTroopRoster())
        {
            if (item.Character == quest._troubleCharacterObject)
            {
                troopCount = item.Number;
                break;
            }
        }

        quest._companyTroopCount = troopCount;
        return troopCount;
    }

    public bool TryCaptureCompanyEnemyParty(Hero owner, out MobileParty companyOfTroubleParty)
    {
        companyOfTroubleParty = null;
        if (owner?.Issue?.IssueQuest is not LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest quest) return false;

        companyOfTroubleParty = quest._companyOfTroubleParty;
        return companyOfTroubleParty != null;
    }

    public void ForceCompanyEnemyParty(Hero owner, MobileParty companyOfTroubleParty)
    {
        if (owner?.Issue?.IssueQuest is not LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest quest) return;

        quest._companyOfTroubleParty = companyOfTroubleParty;
        quest._battleWillStart = true;
    }

    public MobileParty CreateCompanyEnemyPartyOnServer(Hero owner, CampaignVec2 position, int troopCount)
    {
        if (owner?.Issue?.IssueQuest is not LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest quest) return null;
        if (quest._companyOfTroubleParty != null) return quest._companyOfTroubleParty;

        var settlement = SettlementHelper.FindRandomSettlement(x => x.IsHideout);
        if (settlement == null) return null;

        var party = BanditPartyComponent.CreateBanditParty(
            "company_of_trouble_" + quest.StringId, settlement.OwnerClan, settlement.Hideout, isBossParty: false, null, position);
        party.MemberRoster.AddToCounts(quest._troubleCharacterObject, troopCount);
        party.Party.SetCustomName(new TextObject("{=PV7RHgUl}Company of Trouble"));
        party.SetPartyUsedByQuest(isActivelyUsed: true);

        ForceCompanyEnemyParty(owner, party);

        return party;
    }
}
