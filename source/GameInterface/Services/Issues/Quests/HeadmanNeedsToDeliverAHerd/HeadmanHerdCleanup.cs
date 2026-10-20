using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;
using Behavior = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal interface IHeadmanHerdCleanup : IGameAbstraction
{
    void Remember(IssueBase issue, MobileParty party);
    bool HasRememberedParty(IssueBase issue);
    void RememberGiverDeath(IssueBase issue);
    IReadOnlyCollection<IssueBase> PendingGiverDeaths { get; }
    IReadOnlyCollection<IssueBase> RememberedIssues { get; }
    void ForgetGiverDeath(IssueBase issue);
    bool ReturnAlternativeTroops(IssueBase issue);
    void RemoveRemaining(Quest quest);
    void Finalize(IssueBase issue);
    void SyncData(IDataStore dataStore);
}

internal class HeadmanHerdCleanup : IHeadmanHerdCleanup
{
    private Dictionary<string, MobileParty> parties = new Dictionary<string, MobileParty>();
    private Dictionary<string, string> questIssueIds = new Dictionary<string, string>();
    private readonly HashSet<IssueBase> pendingGiverDeaths = new HashSet<IssueBase>();
    private readonly Dictionary<string, IssueBase> rememberedIssues = new Dictionary<string, IssueBase>();
    public IReadOnlyCollection<IssueBase> PendingGiverDeaths => pendingGiverDeaths;
    public IReadOnlyCollection<IssueBase> RememberedIssues => rememberedIssues.Values;

    public void Remember(IssueBase issue, MobileParty party)
    {
        if (string.IsNullOrEmpty(issue.StringId)) return;
        parties[issue.StringId] = party;
        rememberedIssues[issue.StringId] = issue;
        if (!string.IsNullOrEmpty(issue.IssueQuest?.StringId))
            questIssueIds[issue.IssueQuest.StringId] = issue.StringId;
    }

    public void RememberGiverDeath(IssueBase issue) => pendingGiverDeaths.Add(issue);
    public void ForgetGiverDeath(IssueBase issue) => pendingGiverDeaths.Remove(issue);

    public bool HasRememberedParty(IssueBase issue) => !string.IsNullOrEmpty(issue.StringId) && parties.ContainsKey(issue.StringId);

    public bool ReturnAlternativeTroops(IssueBase issue)
    {
        if (string.IsNullOrEmpty(issue.StringId) || !parties.TryGetValue(issue.StringId, out var party) || party == null)
            return false;

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
            rememberedIssues.Remove(issue.StringId);
        }
    }

    public void RemoveRemaining(Quest quest)
    {
        var issueId = IssueManager.GetIssueOfQuest(quest)?.StringId;
        if (issueId == null && !string.IsNullOrEmpty(quest.StringId))
            questIssueIds.TryGetValue(quest.StringId, out issueId);

        if (parties.TryGetValue(issueId ?? string.Empty, out var party) && party != null)
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

    public void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("coop_herd_cleanup_parties", ref parties);
        dataStore.SyncData("coop_herd_cleanup_quest_issue_ids", ref questIssueIds);
        if (parties == null || ModInformation.IsClient) parties = new Dictionary<string, MobileParty>();
        if (questIssueIds == null || ModInformation.IsClient) questIssueIds = new Dictionary<string, string>();
        pendingGiverDeaths.Clear();
        rememberedIssues.Clear();
    }
}

[HarmonyPatch(typeof(Behavior), nameof(Behavior.SyncData))]
internal class HeadmanHerdCleanupSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) cleanup.SyncData(dataStore);
    }
}

