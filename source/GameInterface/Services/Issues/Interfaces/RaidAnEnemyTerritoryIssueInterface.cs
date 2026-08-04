using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection/direct-field access <see cref="Patches.RaidAnEnemyTerritoryIssueCreationPatch"/>,
/// <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/> and
/// <see cref="Handlers.RaidAnEnemyTerritoryIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue"/>.
///
/// CREATION: unlike <c>TheConquestOfSettlementIssue</c>/<c>CaravanAmbushIssue</c>, this Issue's real public ctor
/// takes ONLY <c>(Hero issueGiver)</c> - the genuinely RNG-dependent pick
/// (<c>Kingdom.All.Where(k =&gt; k.IsAtWarWith(IssueOwner.MapFaction)).GetRandomElementInefficiently()</c>,
/// confirmed by decompile) happens INSIDE the ctor and is stored in a <c>private readonly Kingdom
/// _enemyKingdom</c> field with no ctor overload to force it in directly. So
/// <see cref="ConstructReplicated"/> must construct via the real ctor (rolling a throwaway, possibly-wrong
/// local pick) and then force-overwrite <c>_enemyKingdom</c> via reflection (readonly fields can only be
/// assigned from within their own declaring type's constructor at the C# language level - even once
/// Publicized to public accessibility, a reflective <see cref="FieldInfo.SetValue"/> is the only way to
/// overwrite it after construction, same technique <c>CaravanAmbushIssueInterface</c> already uses for its own
/// private Quest-level fields).
///
/// ACCEPT TIME: <c>GenerateIssueQuest(questId)</c> forwards <c>IssueOwner</c>/a fixed 60-day duration/the
/// already-replicated <c>_enemyKingdom</c> - all frozen by accept time once creation is captured/forced above -
/// see this type's entry in <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>.
/// <c>IsThereAlternativeSolution</c> is <c>false</c> (confirmed by decompile), so this type is deliberately NOT
/// in <see cref="GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible"/> and needs no
/// <c>NewIssueTypesAlternativeSolutionPatches</c> registration.
///
/// RAID PROGRESS: <see cref="TryApplyRaidedVillage"/> is the single choke point both
/// <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/> (server, applying its own already-correct
/// local mirror directly) and <see cref="Handlers.RaidAnEnemyTerritoryIssueHandler"/>'s network handler (every
/// OTHER peer, applying the server's broadcast) call to faithfully reimplement the mutating half of vanilla's
/// own <c>OnRaidCompleted</c> (<c>_raidedVillages.Add</c>/<c>_raidedVillagesTrackLog.UpdateCurrentProgress</c>/
/// the "go turn it in" quick-info toast) - see that patch's own doc comment for why vanilla's own per-quest-
/// instance listener can never be trusted to run this itself. The quick-info toast is deliberately gated to
/// <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/> - it is a local-UI-only nicety with no synced
/// equivalent in this codebase, and unconditionally calling it (as vanilla's own single-process code safely
/// could) would show it on the WRONG peer's screen when the module-level listener runs server-side but the
/// real owner is a remote client.
/// </summary>
public interface IRaidAnEnemyTerritoryIssueInterface : IGameAbstraction
{
    /// <summary>Reads the rolled <c>_enemyKingdom</c> off an already-constructed issue (direct field read - safe
    /// even though the field is readonly, since C#'s readonly restriction only applies to writes). Returns
    /// false if missing, which should never happen for a real instance.</summary>
    bool TryCaptureEnemyKingdom(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue issue, out Kingdom enemyKingdom);

    /// <summary>
    /// Builds a <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue"/> via its real public
    /// ctor (which only takes the owner Hero), then force-overwrites its readonly <c>_enemyKingdom</c> field via
    /// reflection with the server-captured value - see the type doc comment for why a ctor-argument pass isn't
    /// possible here, unlike <c>TheConquestOfSettlementIssue</c>/<c>CaravanAmbushIssue</c>. Does not register it
    /// with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue ConstructReplicated(Hero owner, Kingdom enemyKingdom);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling the enemy kingdom on) a new one - same technique as
    /// <see cref="TheConquestOfSettlementIssueInterface.RegisterReplicated"/>.
    /// </summary>
    void RegisterReplicated(Hero owner, RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue issue);

    /// <summary>
    /// Faithful, idempotent reimplementation of the mutating half of vanilla's own
    /// <c>OnRaidCompleted</c> - adds <paramref name="settlement"/> to <paramref name="questGiver"/>'s current
    /// <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest"/>'s <c>_raidedVillages</c> (if not
    /// already present), updates the discrete journal-log progress tracker (null-safe - only the genuine
    /// accepter's own machine ever has a non-null <c>_raidedVillagesTrackLog</c>, since it's only ever populated
    /// inside the live-dialogue-only <c>QuestAcceptedByPlayerConsequences</c>, never by the generic accept
    /// mirror), and shows the "go turn it in" quick-info toast ONLY on the recorded owner's own local machine.
    /// Returns false (no-op) if the hero has no matching quest, the quest is already finalized, or the
    /// settlement was already recorded (a genuine duplicate delivery of the same broadcast).
    /// </summary>
    bool TryApplyRaidedVillage(Hero questGiver, Settlement settlement);
}

/// <inheritdoc cref="IRaidAnEnemyTerritoryIssueInterface"/>
public class RaidAnEnemyTerritoryIssueInterface : IRaidAnEnemyTerritoryIssueInterface
{
    private static readonly FieldInfo EnemyKingdomField =
        AccessTools.Field(typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue), "_enemyKingdom");

    public bool TryCaptureEnemyKingdom(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue issue, out Kingdom enemyKingdom)
    {
        enemyKingdom = null;
        if (issue == null) return false;

        enemyKingdom = (Kingdom)EnemyKingdomField.GetValue(issue);
        return enemyKingdom != null;
    }

    public RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue ConstructReplicated(Hero owner, Kingdom enemyKingdom)
    {
        var issue = new RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue(owner);
        EnemyKingdomField.SetValue(issue, enemyKingdom);
        return issue;
    }

    public void RegisterReplicated(Hero owner, RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public bool TryApplyRaidedVillage(Hero questGiver, Settlement settlement)
    {
        if (settlement == null) return false;
        if (questGiver?.Issue?.IssueQuest is not RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest quest) return false;
        if (quest.IsFinalized) return false;
        if (quest._raidedVillages.Contains(settlement)) return false;

        quest._raidedVillages.Add(settlement);
        quest._raidedVillagesTrackLog?.UpdateCurrentProgress(quest._raidedVillages.Count);

        if (quest._raidedVillages.Count >= 3 && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(questGiver))
        {
            var textObject = new TextObject("{=VM9xDun7}You have successfully raided target villages. Go back to {QUEST_GIVER.LINK} to get your reward.");
            StringHelpers.SetCharacterProperties("QUEST_GIVER", quest.QuestGiver.CharacterObject, textObject);
            MBInformationManager.AddQuickInformation(textObject);
        }

        return true;
    }
}

/// <summary>
/// Bug fix shared root-cause resolver (same shape as <see cref="TheConquestOfSettlementIssueOwnershipResolver"/>):
/// resolves the REAL connected player who accepted a
/// <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue"/>'s quest, from the same recorded-
/// ownership registry (<see cref="VillageNeedsToolsIssueOwnership"/>) every other ownership fix in this codebase
/// already relies on, instead of the ambient <c>MobileParty.MainParty</c> vanilla's own <c>OnRaidCompleted</c>
/// reads via <c>MapEvent.IsPlayerMapEvent</c>/<c>MapEvent.PlayerSide</c> - see
/// <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/> for the full derivation and the one call site
/// this fixes.
/// </summary>
internal static class RaidAnEnemyTerritoryIssueOwnershipResolver
{
    /// <summary>
    /// Resolves the real accepting player's Hero + MobileParty for <paramref name="questGiver"/>'s issue (the
    /// dictionary key <see cref="VillageNeedsToolsIssueOwnership"/> is keyed by - the NPC issue-giver Hero, NOT
    /// the accepter). Returns false (both outputs null) if no owner has been recorded yet, or the recorded
    /// player/their Hero can no longer be resolved (e.g. disconnected) - callers must fail closed in that case,
    /// matching <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/>'s own fail-closed convention.
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
