using Common.Util;
using Helpers;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
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
/// Wraps the reflection/replay access <see cref="Patches.SmugglersIssueCreationPatch"/>,
/// <see cref="Patches.SmugglersPartySpawnGatePatch"/> and <see cref="Handlers.SmugglersIssueHandler"/> need to
/// capture and authoritatively replicate a <see cref="SmugglersIssueBehavior.SmugglersIssue"/>.
///
/// CREATION: <c>SmugglersIssue</c>'s ctor takes <c>(Hero, KeyValuePair&lt;Settlement, Settlement&gt;)</c>
/// directly - the target/origin settlement pair is picked server-side (creation is already fully
/// server-authoritative - see <see cref="Patches.IssueManagerCreateNewIssuePatches"/> - via
/// <c>SmugglersIssueBehavior.ConditionsHold</c>'s <c>GetRandomElementInefficiently()</c> roll, a genuine
/// per-client-divergent pick if independently re-rolled), so unlike every other bespoke interface in this
/// family, <see cref="ConstructReplicated"/> needs NO reflection at all - the real public ctor accepts both
/// captured settlements directly. <see cref="TryCaptureFields"/> still needs plain field reads (both fields are
/// <c>readonly</c>, but reading a readonly field from outside its declaring type is fine - Publicize just makes
/// them visible; only WRITING a readonly field from outside needs reflection, and nothing here ever does that).
///
/// ACCEPT TIME: <c>GenerateIssueQuest</c> forwards <c>IssueOwner</c>/the two frozen settlements/
/// <c>base.IssueDifficultyMultiplier</c>/a fixed 20-day duration/<c>RewardGold</c> - <c>RewardGold</c> and
/// <c>IssueDifficultyMultiplier</c> are both pure functions of the single shared <c>Campaign.Current.PlayerProgress</c>
/// value (confirmed against the decompiled <c>DefaultIssueModel.GetIssueDifficultyMultiplier</c> - not
/// Hero/party-relative), so a bare replay of <c>IssueManager.StartIssueQuest</c> on every peer lands on a
/// byte-identical <c>SmugglersIssueQuest</c> - see <c>SmugglersIssueBehavior.SmugglersIssue</c>'s entry in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>. No bespoke accept-time
/// capture/force-write is needed for THIS quest type's scalar fields at all - the one genuinely novel mechanic
/// this interface exists for is the party spawn below.
///
/// PARTY SPAWN (the real, independently-verified gap this quest needed its own infrastructure for - see
/// <see cref="Patches.SmugglersPartySpawnGatePatch"/>'s doc comment for the full derivation): <c>_smugglerParty</c>
/// is only ever set by <c>QuestAcceptedConsequences</c>'s call to the private <c>CreateSmugglerParty()</c>,
/// itself only ever reached from the genuine accepter's own live conversation (Category B in the design doc -
/// never replayed by <see cref="GenericAcceptMirrorIssueTypes"/>'s mirror). <c>CreateSmugglerParty()</c> calls
/// <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c>, whose inner <c>new CustomPartyComponent(...)</c>
/// is HARD-BLOCKED on a client (<c>CustomPartyComponentLifetimePatches.Prefix</c> returns false there), and the
/// resulting <c>MobileParty</c> itself - not blocked, just silently orphaned (never registered/synced - see
/// <c>GameInterface.Registry.Auto.LifetimePatches&lt;MobileParty&gt;.CreatePrefix</c>, which only logs an error
/// on a client, it doesn't stop the constructor) - would be a split-brain ghost only the accepting client can
/// see. <see cref="CreateReplicatedSmugglerParty"/> is a faithful, parameterized reimplementation of
/// <c>CreateSmugglerParty()</c>'s body, run ONLY on the server (either because the server itself is the genuine
/// accepter, or because it's servicing a client's forwarded <c>RequestSmugglerPartySpawn</c>) so the real
/// <c>MobileParty</c>/<c>CustomPartyComponent</c> construction always happens on the one machine where it
/// isn't blocked, and the resulting party auto-replicates to every client via the same
/// <c>GameInterface.Registry.Auto</c> AutoRegistry machinery any other genuine server-side party creation
/// already rides (see <c>MobilePartyRegistry</c>) - no bespoke party-content sync needed here, only a small
/// "link this already-synced party to this quest's <c>_smugglerParty</c> field" message
/// (<c>NetworkSmugglersPartySpawned</c>), the same shape as the existing
/// <c>PartyComponentMobilePartyUpdated</c>/<c>NetworkPartyComponentMobilePartyUpdated</c> precedent.
///
/// Parameterized rather than a bare reflective replay of the real private method specifically because of
/// <c>desiredMenCount</c>/<c>customPartyBaseSpeed</c>, BOTH derived from <c>MobileParty.MainParty</c> inside the
/// real method's own body - reading the SERVER's own MainParty there would be wrong whenever the genuine
/// accepter is a remote client (the exact "capture the resulting real troop count/composition... on the
/// accepting peer's own machine" hazard flagged going into this task). <see cref="Patches.SmugglersPartySpawnGatePatch"/>
/// captures both values from the ACCEPTER's own <c>MobileParty.MainParty</c> (whichever machine that
/// genuinely is - the server's own accept path never diverges from vanilla since it always was correct there)
/// before any blocking/forwarding decision, and this method takes them as parameters instead of re-deriving
/// them locally. Everything else in the real method - <c>MBRandom</c> rolls inside
/// <c>MobilePartyHelper.FillPartyManuallyAfterCreation</c>, item selection in the private
/// <c>GiveGoodsToParty</c> - only ever runs once, server-side, so its result is faithfully captured by the
/// AutoRegistry sync as-is; no divergence risk since no OTHER peer ever independently re-rolls it.
/// </summary>
public interface ISmugglersIssueInterface : IGameAbstraction
{
    /// <summary>Reads the two rolled/picked settlements off an already-constructed issue (direct field
    /// reads - see the type doc comment on why no reflection is needed here). Returns false if either
    /// settlement is missing, which should never happen for a real instance.</summary>
    bool TryCaptureFields(
        SmugglersIssueBehavior.SmugglersIssue issue,
        out Settlement targetSettlement,
        out Settlement originSettlement);

    /// <summary>
    /// Builds a <see cref="SmugglersIssueBehavior.SmugglersIssue"/> via its real public ctor, which takes both
    /// settlements directly - no field-forcing/reflection needed (see the type doc comment). Does not register
    /// it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    SmugglersIssueBehavior.SmugglersIssue ConstructReplicated(Hero owner, Settlement targetSettlement, Settlement originSettlement);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead
    /// of constructing (and re-rolling the settlement pair on) a new one - same technique as
    /// <see cref="VillageNeedsToolsIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, SmugglersIssueBehavior.SmugglersIssue issue);

    /// <summary>Reads <c>_smugglerParty</c> off <paramref name="owner"/>'s current
    /// <see cref="SmugglersIssueBehavior.SmugglersIssueQuest"/>. Returns false if the hero has no quest of this
    /// type yet, or the party hasn't been spawned on this peer yet.</summary>
    bool TryCaptureSmugglerParty(Hero owner, out MobileParty party);

    /// <summary>Force-writes <c>_smugglerParty</c> (reflection - see the type doc comment) onto
    /// <paramref name="owner"/>'s current quest, so every peer's own mirror references the SAME
    /// already-AutoRegistry-synced <see cref="MobileParty"/> the server genuinely created. A no-op if the
    /// owner has no <see cref="SmugglersIssueBehavior.SmugglersIssueQuest"/> yet.</summary>
    void ForceSmugglerParty(Hero owner, MobileParty party);

    /// <summary>
    /// The central mechanism (see the type doc comment): a faithful, parameterized reimplementation of the
    /// real, private <c>SmugglersIssueQuest.CreateSmugglerParty()</c>, run on the server against
    /// <paramref name="owner"/>'s current quest, using <paramref name="desiredMenCount"/>/
    /// <paramref name="customPartyBaseSpeed"/> in place of the real method's own (server-side-wrong-when-a-
    /// remote-client-accepted) <c>MobileParty.MainParty</c> reads. Also force-writes the result onto
    /// <c>_smugglerParty</c> before returning it. Returns null if the owner has no
    /// <see cref="SmugglersIssueBehavior.SmugglersIssueQuest"/> yet, or its target/origin settlements are
    /// missing.
    /// </summary>
    MobileParty CreateReplicatedSmugglerParty(Hero owner, int desiredMenCount, float customPartyBaseSpeed);
}

/// <inheritdoc cref="ISmugglersIssueInterface"/>
public class SmugglersIssueInterface : ISmugglersIssueInterface
{
    private static readonly FieldInfo SmugglerPartyField =
        AccessTools.Field(typeof(SmugglersIssueBehavior.SmugglersIssueQuest), "_smugglerParty");

    private static readonly MethodInfo GetAdditionalVisualsForPartyMethod =
        AccessTools.Method(typeof(SmugglersIssueBehavior.SmugglersIssueQuest), "GetAdditionalVisualsForParty");
    private static readonly MethodInfo GiveGoodsToPartyMethod =
        AccessTools.Method(typeof(SmugglersIssueBehavior.SmugglersIssueQuest), "GiveGoodsToParty");
    private static readonly MethodInfo InitializePartyStateMethod =
        AccessTools.Method(typeof(SmugglersIssueBehavior.SmugglersIssueQuest), "InitializePartyState");

    public bool TryCaptureFields(
        SmugglersIssueBehavior.SmugglersIssue issue,
        out Settlement targetSettlement,
        out Settlement originSettlement)
    {
        targetSettlement = null;
        originSettlement = null;
        if (issue == null) return false;

        targetSettlement = issue._targetSettlement;
        originSettlement = issue._originSettlement;

        return targetSettlement != null && originSettlement != null;
    }

    public SmugglersIssueBehavior.SmugglersIssue ConstructReplicated(Hero owner, Settlement targetSettlement, Settlement originSettlement)
    {
        return new SmugglersIssueBehavior.SmugglersIssue(
            owner, new System.Collections.Generic.KeyValuePair<Settlement, Settlement>(targetSettlement, originSettlement));
    }

    public void RegisterReplicated(Hero owner, SmugglersIssueBehavior.SmugglersIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(SmugglersIssueBehavior.SmugglersIssue), IssueBase.IssueFrequency.Rare);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public bool TryCaptureSmugglerParty(Hero owner, out MobileParty party)
    {
        party = null;
        if (owner?.Issue?.IssueQuest is not SmugglersIssueBehavior.SmugglersIssueQuest quest) return false;

        party = (MobileParty)SmugglerPartyField.GetValue(quest);
        return party != null;
    }

    public void ForceSmugglerParty(Hero owner, MobileParty party)
    {
        if (owner?.Issue?.IssueQuest is not SmugglersIssueBehavior.SmugglersIssueQuest quest) return;

        SmugglerPartyField.SetValue(quest, party);
    }

    public MobileParty CreateReplicatedSmugglerParty(Hero owner, int desiredMenCount, float customPartyBaseSpeed)
    {
        if (owner?.Issue?.IssueQuest is not SmugglersIssueBehavior.SmugglersIssueQuest quest) return null;

        var originSettlement = quest._originSettlement;
        var targetSettlement = quest._targetSettlement;
        if (originSettlement == null || targetSettlement == null) return null;

        var name = new TextObject("{=3dhAfC4k}Smugglers of {ORIGIN_SETTLEMENT}");
        name.SetTextVariable("ORIGIN_SETTLEMENT", originSettlement.Name);

        var visualArgs = new object[] { originSettlement.Culture, null, null };
        GetAdditionalVisualsForPartyMethod.Invoke(quest, visualArgs);
        var mountStringId = (string)visualArgs[1];
        var harnessStringId = (string)visualArgs[2];

        var nearestHideout = SettlementHelper.FindNearestHideoutToSettlement(originSettlement, MobileParty.NavigationType.Default);
        var randomCaravanTemplate = CaravanHelper.GetRandomCaravanTemplate(originSettlement.Culture, isElite: false, isLand: true);

        // Deliberately NOT wrapped in AllowedThread - this must look like a genuine, novel server-side
        // creation (same as the host's own real accept path already does, unwrapped) so
        // CustomPartyComponentLifetimePatches / GameInterface.Registry.Auto's MobileParty AutoRegistry both
        // take their real "server created this for the first time, assign an id and broadcast it" branch
        // instead of the "this is a replay of an already-synced object" branch AllowedThread signals.
        var party = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
            originSettlement.GatePosition,
            0.1f,
            originSettlement,
            name,
            Clan.BanditFactions.FirstOrDefault(faction => faction.Culture == nearestHideout.Settlement.Culture),
            TroopRoster.CreateDummyTroopRoster(),
            TroopRoster.CreateDummyTroopRoster(),
            null,
            mountStringId,
            harnessStringId,
            customPartyBaseSpeed,
            avoidHostileActions: true);

        MobilePartyHelper.FillPartyManuallyAfterCreation(party, randomCaravanTemplate, desiredMenCount);

        var character = MBObjectManager.Instance.GetObject<CharacterObject>("nervous_caravanmaster_" + MBRandom.RandomInt(1, 4));
        party.MemberRoster.AddToCounts(character, 1, insertAtFront: true);

        GiveGoodsToPartyMethod.Invoke(quest, new object[] { party });
        InitializePartyStateMethod.Invoke(quest, new object[] { party });

        party.SetPartyUsedByQuest(isActivelyUsed: true);

        SmugglerPartyField.SetValue(quest, party);

        return party;
    }
}
