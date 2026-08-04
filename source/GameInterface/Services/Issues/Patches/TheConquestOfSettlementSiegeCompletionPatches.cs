using Common;
using Common.Logging;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug 1 fix - <c>TheConquestOfSettlementIssueQuest.OnSiegeCompleted</c>.
///
/// Root cause (confirmed by decompile + this repo's own <c>MapEventPatches.Prefix_FinalizeEventAux</c>): the
/// vanilla listener's trigger, <c>MapEvent.FinalizeEventAux()</c>, only ever runs its real body on the coop
/// SERVER's own process - a client's own call is intercepted and never reaches the original (see
/// <c>MapEventPatches.Prefix_FinalizeEventAux</c>, gated on <c>ModInformation.IsServer</c>). Separately,
/// <c>QuestBase.RegisterEvents()</c> - which is what wires this quest's own <c>OnSiegeCompleted</c> listener at
/// all - is only ever called from <c>QuestBase.StartQuest()</c>, itself only ever reached from this quest's
/// <c>OfferDialogFlow</c> accept <c>Consequence</c> (a live dialogue callback, i.e. only on the genuine
/// accepter's own machine - confirmed: <c>TheConquestOfSettlementIssueQuest</c>'s own constructor does NOT call
/// <c>RegisterEvents()</c>, and the generic accept-mirror replay every OTHER peer uses
/// (<c>IssueManager.StartIssueQuest</c> -&gt; <c>IssueBase.StartIssueWithQuest</c> -&gt; <c>GenerateIssueQuest</c>)
/// only ever constructs the Quest object, never calls <c>StartQuest()</c>). Net effect: this listener is only
/// EVER reachable at all when the genuine accepter happens to be the server operator - on any other topology
/// (a dedicated server, or a listen server whose operator isn't the one who accepted this specific quest) it
/// silently never fires, and the quest can never resolve via a successful siege. Even in the one reachable
/// case, the check itself (<c>attackerParty == MobileParty.MainParty</c>/
/// <c>InvolvedParties.Contains(PartyBase.MainParty)</c>) reads "this machine's own locally-controlled avatar,"
/// not this quest's real recorded owner - the two only coincide by the accident of the accepter and the server
/// operator being the same connected player.
///
/// Fix (matching <c>BattleMissionStartHandler.ApplyClientAttackHostileConsequences</c>'s established precedent
/// of resolving an identity explicitly instead of reading <c>MobileParty.MainParty</c>/<c>Hero.MainHero</c>
/// ambiently): the vanilla per-quest-instance listener is neutered entirely (it can never reach the true owner
/// on its own) and replaced by ONE server-side, module-level listener, subscribed once per campaign load, that
/// scans every currently-active <see cref="TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue"/>
/// for a matching target settlement and resolves the real owner via
/// <see cref="TheConquestOfSettlementIssueOwnershipResolver"/> (the same
/// <see cref="VillageNeedsToolsIssueOwnership"/> registry every other ownership fix in this codebase already
/// relies on) - reachable and correct regardless of whether the real owner is the server operator, a remote
/// client, or (on a dedicated server) nobody locally at all. <see cref="ApplyQuestSuccess"/>/
/// <see cref="ApplyQuestLesserSuccess"/> are faithful reimplementations of the private
/// <c>QuestSuccess(int)</c>/<c>QuestLesserSuccess()</c> (not a bare direct call) because those methods
/// themselves read <c>Hero.MainHero</c> for the gold/renown payout - on this listener's only reachable process
/// (the server), that would still mean "the server operator's own avatar," not the resolved owner.
/// </summary>
[HarmonyPatch]
internal class TheConquestOfSettlementSiegeCompletionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<TheConquestOfSettlementSiegeCompletionPatches>();

    /// <summary>Neuters vanilla's own per-quest-instance listener entirely - see the type doc comment for why
    /// it can never reliably reach the true owner on its own. All of its logic is replaced by
    /// <see cref="OnSiegeCompleted"/> below.</summary>
    [HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest), "OnSiegeCompleted")]
    [HarmonyPrefix]
    private static bool OnSiegeCompletedPrefix() => false;

    /// <summary>Registered once per campaign load, matching <c>NewIssueTypesAlternativeSolutionPatches</c>'s own
    /// established "postfix the outer <c>CampaignBehaviorBase.RegisterEvents</c>" pattern. Server-only:
    /// <c>SiegeCompletedEvent</c> is only ever actually dispatched on the coop server's own process (see the
    /// type doc comment), so subscribing on a client would just never fire.</summary>
    [HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior), nameof(TheConquestOfSettlementIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(TheConquestOfSettlementIssueBehavior __instance)
    {
        if (ModInformation.IsClient) return;

        CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(__instance, OnSiegeCompleted);
    }

    private static void OnSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool isWin, MapEvent.BattleTypes battleType)
    {
        if (!isWin || attackerParty == null) return;
        if (Campaign.Current?.IssueManager == null) return;

        // Snapshot first: a genuine completion mutates IssueManager.Issues (removes the finalized entry), and
        // MBReadOnlyDictionary's own enumerator doesn't tolerate that mid-iteration - same precaution as
        // NewIssueTypesAlternativeSolutionPatches.OnHourlyTick.
        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (kvp.Value is not TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue) continue;
            if (kvp.Value.IssueQuest is not TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest quest) continue;
            if (quest.IsFinalized) continue;
            if (quest._targetSettlement != siegeSettlement) continue;

            Resolve(quest, siegeSettlement, attackerParty);
        }
    }

    private static void Resolve(
        TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest quest,
        Settlement siegeSettlement,
        MobileParty attackerParty)
    {
        var questGiver = quest.QuestGiver;

        if (attackerParty.MapFaction != questGiver.MapFaction)
        {
            var takenByAnotherFaction = quest.TargetSettlementTakenByAnotherFaction;
            takenByAnotherFaction.SetTextVariable("NEW_OWNER_FACTION", attackerParty.MapFaction.EncyclopediaLinkWithName);
            quest.CompleteQuestWithCancel(takenByAnotherFaction);
            return;
        }

        if (!TheConquestOfSettlementIssueOwnershipResolver.TryResolveOwner(questGiver, out var ownerHero, out var ownerParty))
        {
            // No recorded owner (e.g. a race between accept and this event, which should never happen in
            // practice since accept ownership recording is synchronous with the accept broadcast) - fail safe
            // to the same "not involved" outcome vanilla's own else-branch already gives rather than guessing.
            Logger.Warning("Could not resolve the real owner for The Conquest of a Settlement issue given by {QuestGiver}; treating the successful siege as moot", questGiver);
            quest.CompleteQuestWithCancel(quest.TargetSettlementTakenByPlayerFaction);
            return;
        }

        if (ReferenceEquals(attackerParty, ownerParty))
        {
            ApplyQuestSuccess(quest, ownerHero, 10);
        }
        else if (ownerParty != null && siegeSettlement.Party?.MapEvent?.InvolvedParties?.Contains(ownerParty.Party) == true)
        {
            ApplyQuestLesserSuccess(quest, ownerHero);
        }
        else
        {
            quest.CompleteQuestWithCancel(quest.TargetSettlementTakenByPlayerFaction);
        }
    }

    /// <summary>Faithful reimplementation of the private <c>QuestSuccess(int)</c> - see the type doc comment for
    /// why a bare direct/reflective call isn't safe here: the real method reads <c>Hero.MainHero</c> directly
    /// for the gold payout.</summary>
    private static void ApplyQuestSuccess(
        TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest quest, Hero ownerHero, int boost)
    {
        quest.AddLog(quest.QuestSuccessLog);
        quest.RelationshipChangeWithQuestGiver = 5;
        GiveGoldAction.ApplyBetweenCharacters(null, ownerHero, quest.RewardGold);
        foreach (var settlement in quest.QuestGiver.Clan.Settlements.Where(x => x.IsFortification))
        {
            settlement.Town.Security += boost;
            settlement.Town.Loyalty += boost;
        }
        quest.CompleteQuestWithSuccess();
    }

    /// <summary>Faithful reimplementation of the private <c>QuestLesserSuccess()</c> - same reasoning as
    /// <see cref="ApplyQuestSuccess"/>.</summary>
    private static void ApplyQuestLesserSuccess(
        TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest quest, Hero ownerHero)
    {
        var log = quest.QuestLesserSuccessLog;
        var reward = quest.RewardGold / 4;
        log.SetTextVariable("LESSER_REWARD", reward);
        quest.AddLog(log);
        GainRenownAction.Apply(ownerHero, 5f);
        GiveGoldAction.ApplyBetweenCharacters(null, ownerHero, reward);
        quest.RelationshipChangeWithQuestGiver = 1;
        foreach (var settlement in quest.QuestGiver.Clan.Settlements.Where(x => x.IsFortification))
        {
            settlement.Town.Security += 2f;
            settlement.Town.Loyalty += 2f;
        }
        quest.CompleteQuestWithSuccess();
    }
}
