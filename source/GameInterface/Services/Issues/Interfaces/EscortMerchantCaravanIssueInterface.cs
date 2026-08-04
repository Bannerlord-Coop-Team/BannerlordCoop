using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.EscortMerchantCaravanIssueCreationPatch"/>,
/// <see cref="Patches.EscortMerchantCaravanPartySpawnGatePatch"/> and
/// <see cref="Handlers.EscortMerchantCaravanIssueHandler"/> need to capture and authoritatively replicate an
/// <see cref="EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue"/> - see
/// doc/EscortMerchantCaravan_Design_v2.md for the full design this implements.
///
/// CREATION: the ctor (<c>EscortMerchantCaravanIssue(Hero issueOwner)</c>) rolls
/// <c>_companionRewardRandom = MBRandom.RandomInt(3, 10)</c> - a genuine, per-client-divergent creation-time
/// dice roll, same shape as <c>LordNeedsHorsesIssue</c>'s own <c>_mountObjectToBeDelivered</c> capture.
/// <see cref="TryCaptureCompanionRewardRandom"/>/<see cref="ConstructReplicated"/> follow that exact same
/// "build via the real ctor (which re-rolls it locally and wrongly), then force-overwrite the one field with
/// the server's authoritative value" shape.
///
/// IMPORTANT CORRECTION vs the design doc's own §5 (independently re-verified against the decompiled source,
/// not trusted, per this session's standing lesson): the design doc claims <c>_companionRewardRandom</c> "feeds
/// <c>RewardGold</c>, which <c>GenerateIssueQuest</c> forwards into the Quest's own <c>rewardGold</c>
/// constructor parameter." That is WRONG - <c>GenerateIssueQuest</c> actually forwards
/// <c>DailyQuestRewardGold</c> (a pure, deterministic function of <c>IssueDifficultyMultiplier</c>, NOT
/// involving <c>_companionRewardRandom</c> at all) into the Quest ctor. <c>_companionRewardRandom</c> only ever
/// feeds the ISSUE-level <c>RewardGold</c> override (<c>Math.Min(DailyQuestRewardGold * _companionRewardRandom,
/// 8000)</c>), which is consumed at exactly ONE real call site in the whole decompiled type:
/// <c>IssueBase.AlternativeSolutionEndWithSuccess()</c>'s <c>GiveGoldAction.ApplyBetweenCharacters(null,
/// Hero.MainHero, RewardGold)</c> - reached only via <c>IssueBase.CompleteIssueWithAlternativeSolution()</c>,
/// which is ALREADY gated to the recorded owner by
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/> once this type is added to
/// <see cref="GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible"/> (done as part of this pass).
/// The capture is still needed (a non-owner's own independently-rolled copy would show a different number in
/// its OWN issue-browsing UI even though it can never pay itself), but it is narrower than the design doc
/// implied: the Quest's own <c>TotalRewardGold</c>/main-quest-success payout (<c>SuccessConsequences()</c>) uses
/// <c>QuestBase.RewardGold</c> (the Quest ctor's own, unrelated, fully-deterministic <c>rewardGold</c> param) -
/// completely unaffected by <c>_companionRewardRandom</c>.
///
/// ACCEPT TIME: <c>GenerateIssueQuest(questId)</c> forwards <c>IssueOwner</c>/a fixed 30-day due time/
/// <c>IssueDifficultyMultiplier</c>/<c>DailyQuestRewardGold</c> - all pure functions of already-shared state, so
/// once creation is captured/forced, a bare replay of <c>IssueManager.StartIssueQuest</c> lands on a
/// byte-identical <c>EscortMerchantCaravanIssueQuest</c> on every peer - see this type's entry in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>.
///
/// CARAVAN PARTY SPAWN: <c>SpawnCaravan()</c> is a separate, individually-Harmony-patchable private method
/// (unlike Caravan Ambush's fully-inline <c>OnQuestAccepted()</c>), called from the real
/// <c>QuestAcceptedConsequences()</c> Consequence (a live <c>OfferDialogFlow.Consequence</c>, Category B - only
/// ever reached on the genuine accepter's own machine). It calls
/// <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster(...)</c>, hitting the same client-authority gap
/// (<c>CustomPartyComponentLifetimePatches</c> hard-blocks the component ctor on a client) every other
/// CustomPartyComponent-based party in this family (Smugglers, Merchant Army of Poachers, Gang Leader Needs
/// Weapons' guards party) already needed a gate for.
///
/// Independently verified (per this task's own standing lesson): unlike Caravan Ambush
/// (<c>accepterMainPartySpeed</c>) or Merchant Army of Poachers (<c>MobileParty.MainParty</c> for the bandit
/// hideout/culture pick), <c>SpawnCaravan()</c>'s WHOLE call graph
/// (<c>InitializeCaravanOnCreation</c>/<c>GetAdditionalVisualsForParty</c>) reads ONLY
/// <c>base.QuestGiver</c>-derived state (<c>CurrentSettlement</c>, <c>Culture</c>, <c>Clan</c>) and Quest-
/// internal, already-frozen fields (<c>_difficultyMultiplier</c>) - it never once reads
/// <c>MobileParty.MainParty</c> or any other accepter-local value. This means, unlike every other party-spawn
/// gate in this family, NO captured/forwarded accepter-derived parameter is needed at all - the server can
/// reproduce the real body byte-for-byte with zero divergence risk, the same "first among this family" shape
/// <c>GangLeaderNeedsWeaponsIssueQuestBehavior.CreateGuardsParty()</c> established. Its cosmetic one-shot RNG
/// (mount/harness pick via <c>GetAdditionalVisualsForParty</c>'s <c>MBRandom.RandomFloat</c>, caravan-template
/// pick via <c>CaravanHelper.GetRandomCaravanTemplate</c>) is a single, canonical, server-side-only roll,
/// faithfully captured by the AutoRegistry sync of the resulting <see cref="MobileParty"/> as-is - no separate
/// capture needed, same reasoning as every other one-shot party-creation RNG roll in this family.
///
/// Because of this zero-accepter-state-dependency property, <see cref="SpawnCaravanOnServer"/> does NOT
/// hand-reimplement <c>SpawnCaravan()</c>'s body (unlike <c>IMerchantArmyOfPoachersIssueInterface.CreatePoachersPartyOnServer</c>,
/// which needed a deliberate substitution) - it reflectively invokes the REAL private <c>SpawnCaravan()</c>
/// method, the same "faithful reuse, not hand-reimplementation" precedent
/// <c>IGangLeaderNeedsWeaponsIssueInterface.CreateGuardsPartyOnServer</c> established. Because
/// <see cref="Patches.EscortMerchantCaravanPartySpawnGatePatch"/> Harmony-patches <c>SpawnCaravan()</c> itself
/// (not a wrapper), this reflective invoke re-triggers the SAME Postfix that broadcasts a genuine host-owner's
/// own real call - one Harmony choke point covers both paths, so
/// <see cref="Handlers.EscortMerchantCaravanIssueHandler"/> never needs its own separate broadcast call after
/// invoking this method (same shape as <c>GangLeaderNeedsWeaponsIssueHandler.Handle_NetworkGuardsPartySpawnRequest</c>).
///
/// PER-LEG/REPEATED RNG (route selection, bandit-troop composition, troop top-up) is NOT captured here - per
/// the design doc's corrected architecture (§3.4/§4, independently re-verified for every call site: all of
/// <c>TryToFindAndSetTargetToNextSettlement</c>'s route pick, <c>ActivateBanditParty</c>'s
/// <c>MBRandom.ChooseWeighted</c> troop rolls, and <c>OnSettlementEntered</c>'s troop top-up are reached ONLY
/// through the 7 <c>RegisterEvents()</c> listeners plus the Quest's own <c>HourlyTick</c>, all of which
/// <see cref="Patches.EscortMerchantCaravanOwnershipGatePatches"/> gates to the recorded owner), this RNG only
/// ever executes on the single genuine owner's machine - no capture-and-force-write needed, unlike accept-time
/// values.
/// </summary>
public interface IEscortMerchantCaravanIssueInterface : IGameAbstraction
{
    /// <summary>Reads the rolled <c>_companionRewardRandom</c> off an already-constructed issue (reflection -
    /// the field is private). Returns false if the issue is null.</summary>
    bool TryCaptureCompanionRewardRandom(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue issue, out int companionRewardRandom);

    /// <summary>
    /// Builds an <see cref="EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue"/> via its real public
    /// ctor (which independently re-rolls <c>_companionRewardRandom</c>), then force-overwrites that one field
    /// with the server's authoritative value - see the type doc comment. Does not register it with the
    /// <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue ConstructReplicated(Hero owner, int companionRewardRandom);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling <c>_companionRewardRandom</c> on) a new one.
    /// </summary>
    void RegisterReplicated(Hero owner, EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue issue);

    /// <summary>Reads <c>_questCaravanMobileParty</c> off <paramref name="owner"/>'s current
    /// <see cref="EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest"/>. Returns false if the
    /// hero has no quest of this type yet, or the party hasn't been spawned/mirrored on this peer yet.</summary>
    bool TryCaptureCaravanParty(Hero owner, out MobileParty caravanParty);

    /// <summary>Force-writes <c>_questCaravanMobileParty</c> onto <paramref name="owner"/>'s current quest, so
    /// every peer's own mirror references the SAME already-AutoRegistry-synced <see cref="MobileParty"/> the
    /// server genuinely created. A no-op if the owner has no
    /// <see cref="EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest"/> yet.</summary>
    void ForceCaravanParty(Hero owner, MobileParty caravanParty);

    /// <summary>
    /// Server-only: reflectively invokes the REAL, private <c>SpawnCaravan()</c> - a faithful reuse, not a
    /// hand-reimplementation (see the type doc comment for why that's safe here). Idempotent - a no-op
    /// returning the already-existing party if one is already set. Returns null if the owner has no active
    /// quest yet, or its quest giver has no current settlement (should never happen for a real instance).
    /// </summary>
    MobileParty SpawnCaravanOnServer(Hero owner);
}

/// <inheritdoc cref="IEscortMerchantCaravanIssueInterface"/>
public class EscortMerchantCaravanIssueInterface : IEscortMerchantCaravanIssueInterface
{
    private static readonly FieldInfo CompanionRewardRandomField =
        AccessTools.Field(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue), "_companionRewardRandom");
    private static readonly FieldInfo QuestCaravanMobilePartyField =
        AccessTools.Field(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest), "_questCaravanMobileParty");
    private static readonly MethodInfo SpawnCaravanMethod =
        AccessTools.Method(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest), "SpawnCaravan");

    public bool TryCaptureCompanionRewardRandom(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue issue, out int companionRewardRandom)
    {
        companionRewardRandom = 0;
        if (issue == null) return false;

        companionRewardRandom = (int)CompanionRewardRandomField.GetValue(issue);
        return true;
    }

    public EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue ConstructReplicated(Hero owner, int companionRewardRandom)
    {
        // The public ctor is the only way to build one, and it independently re-rolls _companionRewardRandom
        // via MBRandom.RandomInt(3, 10). Build it normally for everything else it sets up, then force the one
        // field to the server's authoritative value.
        var issue = new EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue(owner);

        CompanionRewardRandomField.SetValue(issue, companionRewardRandom);

        return issue;
    }

    public void RegisterReplicated(Hero owner, EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public bool TryCaptureCaravanParty(Hero owner, out MobileParty caravanParty)
    {
        caravanParty = null;
        if (owner?.Issue?.IssueQuest is not EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest quest) return false;

        caravanParty = (MobileParty)QuestCaravanMobilePartyField.GetValue(quest);
        return caravanParty != null;
    }

    public void ForceCaravanParty(Hero owner, MobileParty caravanParty)
    {
        if (owner?.Issue?.IssueQuest is not EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest quest) return;

        QuestCaravanMobilePartyField.SetValue(quest, caravanParty);
    }

    public MobileParty SpawnCaravanOnServer(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest quest) return null;

        if (QuestCaravanMobilePartyField.GetValue(quest) is MobileParty existing && existing != null) return existing;
        if (quest.QuestGiver?.CurrentSettlement == null) return null;

        // Deliberately NOT wrapped in AllowedThread - this must look like a genuine, novel server-side creation
        // (same as the host's own real accept path already does, unwrapped) so
        // CustomPartyComponentLifetimePatches / GameInterface.Registry.Auto's MobileParty AutoRegistry both take
        // their real "server created this for the first time" branch - see
        // IGangLeaderNeedsWeaponsIssueInterface.CreateGuardsPartyOnServer's own doc comment for the precedent
        // this follows. This also re-triggers Patches.EscortMerchantCaravanPartySpawnGatePatch's own Postfix on
        // SpawnCaravan() - see the type doc comment for why that's the intended broadcast mechanism here.
        SpawnCaravanMethod.Invoke(quest, null);

        return (MobileParty)QuestCaravanMobilePartyField.GetValue(quest);
    }
}
