using Common;
using HarmonyLib;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Behavior = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal interface IHeadmanHerdCleanup : IGameAbstraction
{
    bool RememberOwnerParty(IssueBase issue);
    void Remember(IssueBase issue, MobileParty party);
    void RollbackGrant(IssueBase issue, Quest quest);
    void Forget(IssueBase issue);
    bool HasRememberedParty(IssueBase issue);
    bool ReturnAlternativeTroops(IssueBase issue);
    void RemoveRemaining(Quest quest);
    void Finalize(IssueBase issue);
    void SyncData(IDataStore dataStore);
}

internal class HeadmanHerdCleanup : IHeadmanHerdCleanup
{
    private readonly IIssueOwnerResolver owners;
    private Dictionary<string, MobileParty> parties = new Dictionary<string, MobileParty>();
    private Dictionary<string, string> questIssueIds = new Dictionary<string, string>();
    private readonly Dictionary<string, (int HerdCount, int GrainCount)> grantSnapshots = new();

    public HeadmanHerdCleanup(IIssueOwnerResolver owners) => this.owners = owners;

    public bool RememberOwnerParty(IssueBase issue)
    {
        if (!owners.TryResolveOwnerHero(issue, out var owner) || owner.PartyBelongedTo == null)
            return false;

        Remember(issue, owner.PartyBelongedTo);
        return true;
    }

    public void Remember(IssueBase issue, MobileParty party)
    {
        if (string.IsNullOrEmpty(issue.StringId) || party == null) return;
        parties[issue.StringId] = party;
        if (issue.IssueQuest is Quest quest)
        {
            grantSnapshots[issue.StringId] = (
                party.ItemRoster.GetItemNumber(quest._herdTypeToDeliver),
                party.ItemRoster.GetItemNumber(DefaultItems.Grain));
        }
        if (!string.IsNullOrEmpty(issue.IssueQuest?.StringId))
            questIssueIds[issue.IssueQuest.StringId] = issue.StringId;
    }

    public void RollbackGrant(IssueBase issue, Quest quest)
    {
        if (!string.IsNullOrEmpty(issue.StringId) && parties.TryGetValue(issue.StringId, out var party) &&
            grantSnapshots.TryGetValue(issue.StringId, out var snapshot))
        {
            RemoveAdded(party.ItemRoster, quest._herdTypeToDeliver, snapshot.HerdCount);
            RemoveAdded(party.ItemRoster, DefaultItems.Grain, snapshot.GrainCount);
        }

        Forget(issue);
    }

    public bool HasRememberedParty(IssueBase issue) => !string.IsNullOrEmpty(issue.StringId) && parties.ContainsKey(issue.StringId);

    public void Forget(IssueBase issue)
    {
        if (!string.IsNullOrEmpty(issue.IssueQuest?.StringId))
            questIssueIds.Remove(issue.IssueQuest.StringId);
        if (!string.IsNullOrEmpty(issue.StringId))
        {
            parties.Remove(issue.StringId);
            grantSnapshots.Remove(issue.StringId);
        }
    }

    public bool ReturnAlternativeTroops(IssueBase issue)
    {
        // Completion and cancellation establish the current owner's scope, including after heir replacement.
        var party = MobileParty.MainParty;
        if (party == null) return true;

        foreach (var element in issue.AlternativeSolutionSentTroops.GetTroopRoster())
        {
            if (element.Character.IsHero)
                element.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
        }

        party.MemberRoster.Add(issue.AlternativeSolutionSentTroops);
        issue.AlternativeSolutionSentTroops.Clear();
        return true;
    }

    public void Finalize(IssueBase issue)
    {
        if (issue.IssueQuest is Quest quest && quest.IsOngoing)
            RemoveRemaining(quest);
        if (!string.IsNullOrEmpty(issue.IssueQuest?.StringId))
            questIssueIds.Remove(issue.IssueQuest.StringId);
        if (!string.IsNullOrEmpty(issue.StringId))
        {
            parties.Remove(issue.StringId);
            grantSnapshots.Remove(issue.StringId);
        }
    }

    public void RemoveRemaining(Quest quest)
    {
        // This mirrors vanilla RemoveRemainingHorses: normal cancellation removes the requested
        // count, while RollbackGrant uses the pre-grant snapshot to preserve pre-existing stock.
        var issueId = IssueManager.GetIssueOfQuest(quest)?.StringId;
        if (issueId == null && !string.IsNullOrEmpty(quest.StringId))
            questIssueIds.TryGetValue(quest.StringId, out issueId);

        if (!parties.TryGetValue(issueId ?? string.Empty, out var party) || party == null)
            party = MobileParty.MainParty;

        if (party != null)
        {
            var remaining = quest._animalCountToDeliver;
            foreach (var element in party.ItemRoster.ToArray())
            {
                if (element.EquipmentElement.Item != quest._herdTypeToDeliver) continue;
                var removed = Math.Min(remaining, element.Amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -removed);
                remaining -= removed;
                if (remaining == 0) break;
            }
        }
    }

    private static void RemoveAdded(ItemRoster roster, ItemObject item, int before)
    {
        var current = roster.GetItemNumber(item);
        var added = current - before;
        if (added > 0) roster.AddToCounts(item, -added);
    }

    public void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("coop_herd_cleanup_parties", ref parties);
        dataStore.SyncData("coop_herd_cleanup_quest_issue_ids", ref questIssueIds);
        if (parties == null || ModInformation.IsClient) parties = new Dictionary<string, MobileParty>();
        if (questIssueIds == null || ModInformation.IsClient) questIssueIds = new Dictionary<string, string>();
        grantSnapshots.Clear();
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Behavior), nameof(Behavior.SyncData))]
internal class HeadmanHerdCleanupSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) cleanup.SyncData(dataStore);
    }
}

