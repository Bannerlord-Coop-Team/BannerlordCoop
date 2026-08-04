using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the direct field access (see TaleWorlds.CampaignSystem's whole-assembly Publicize in
/// GameInterface.csproj) <see cref="Patches.TheConquestOfSettlementIssueCreationPatch"/> and
/// <see cref="Handlers.TheConquestOfSettlementIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue"/>.
///
/// CREATION: the Issue ctor takes <c>(Hero, Settlement)</c> directly - the target settlement is picked
/// server-side (creation is already fully server-authoritative, see
/// <see cref="Patches.IssueManagerCreateNewIssuePatches"/>) via <c>TheConquestOfSettlementIssueBehavior.ConditionsHold</c>'s
/// <c>mBList.GetRandomElement()</c> pick - genuinely RNG-dependent, the same shape as
/// <c>ICaravanAmbushIssueInterface</c>'s own target-settlement capture - so <see cref="ConstructReplicated"/>
/// needs no reflection at all, the real public ctor accepts the captured settlement directly.
///
/// ACCEPT TIME: <c>GenerateIssueQuest(questId)</c> forwards <c>IssueOwner</c>/the already-replicated
/// <c>_targetSettlement</c>/a fixed 60-day duration/<c>RewardGold</c> (a plain <c>const</c> 20000, not even
/// <c>IssueDifficultyMultiplier</c>-dependent - confirmed by decompile) - all already frozen/deterministic by
/// accept time, so a bare replay of <c>IssueManager.StartIssueQuest</c> on every peer lands on a
/// byte-identical <c>TheConquestOfSettlementIssueQuest</c> - see this type's entry in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>. <c>IsThereAlternativeSolution</c>
/// is <c>false</c> (confirmed by decompile), so this type is deliberately NOT in
/// <see cref="GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible"/> and needs no
/// <c>NewIssueTypesAlternativeSolutionPatches</c> registration.
/// </summary>
public interface ITheConquestOfSettlementIssueInterface : IGameAbstraction
{
    /// <summary>Reads the rolled/picked target settlement off an already-constructed issue (direct field
    /// access). Returns false if the settlement is missing, which should never happen for a real
    /// instance.</summary>
    bool TryCaptureTargetSettlement(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue issue, out Settlement targetSettlement);

    /// <summary>
    /// Builds a <see cref="TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue"/> via its real
    /// public ctor, which takes the settlement directly - no field-forcing/reflection needed (see the type doc
    /// comment). Does not register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue ConstructReplicated(Hero owner, Settlement targetSettlement);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling the target settlement on) a new one - same technique as
    /// <see cref="CaravanAmbushIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue issue);
}

/// <inheritdoc cref="ITheConquestOfSettlementIssueInterface"/>
public class TheConquestOfSettlementIssueInterface : ITheConquestOfSettlementIssueInterface
{
    public bool TryCaptureTargetSettlement(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue issue, out Settlement targetSettlement)
    {
        targetSettlement = null;
        if (issue == null) return false;

        targetSettlement = issue._targetSettlement;
        return targetSettlement != null;
    }

    public TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue ConstructReplicated(Hero owner, Settlement targetSettlement)
    {
        return new TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue(owner, targetSettlement);
    }

    public void RegisterReplicated(Hero owner, TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}

/// <summary>
/// Bug 1 &amp; Bug 2 shared root-cause fix: resolves the REAL connected player who accepted a
/// <see cref="TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue"/>'s quest, from the same
/// recorded-ownership registry (<see cref="VillageNeedsToolsIssueOwnership"/>) every other ownership fix in
/// this codebase already relies on, instead of the ambient <c>MobileParty.MainParty</c>/<c>Hero.MainHero</c> -
/// which, on whichever process happens to run a given listener, means "this machine's own locally-controlled
/// avatar," not necessarily this quest's real owner (see
/// <see cref="Patches.TheConquestOfSettlementSiegeCompletionPatches"/> and
/// <see cref="Patches.TheConquestOfSettlementSettlementOwnerChangedPatch"/> for the two call sites this fixes).
/// </summary>
internal static class TheConquestOfSettlementIssueOwnershipResolver
{
    /// <summary>
    /// Resolves the real accepting player's Hero + MobileParty for <paramref name="questGiver"/>'s issue (the
    /// dictionary key <see cref="VillageNeedsToolsIssueOwnership"/> is keyed by - the NPC issue-giver Hero, NOT
    /// the accepter, see that type's own doc comment). Returns false (both outputs null) if no owner has been
    /// recorded yet, or the recorded player/their Hero can no longer be resolved (e.g. disconnected) - callers
    /// must fail closed in that case, matching <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/>'s
    /// own fail-closed convention.
    /// </summary>
    internal static bool TryResolveOwner(Hero questGiver, out Hero ownerHero, out MobileParty ownerParty)
    {
        ownerHero = null;
        ownerParty = null;

        if (!VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(questGiver, out var controllerId)) return false;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return false;
        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        objectManager.TryGetObject(player.HeroId, out ownerHero);
        objectManager.TryGetObject(player.MobilePartyId, out ownerParty);

        return ownerHero != null;
    }
}
