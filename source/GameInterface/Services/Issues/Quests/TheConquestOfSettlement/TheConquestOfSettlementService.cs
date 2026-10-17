using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

internal interface ITheConquestOfSettlementService
{
    bool TryOpenOwnerScope(IssueBase issue, out IDisposable scope);
    bool IsVisible(QuestBase quest);
    bool IsVisible(JournalLogEntry entry);
    void RecordLocalQuest(Quest quest);
    void RecordQuest(Quest quest);
}

internal sealed class TheConquestOfSettlementService : ITheConquestOfSettlementService
{
    private const string SaveKey = "_coop_the_conquest_of_settlement_quest_owners";
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private static readonly ConcurrentDictionary<string, string> questOwners = new();
    private static bool questOwnersRestored;

    public TheConquestOfSettlementService(
        IObjectManager objectManager,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.objectManager = objectManager;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
    }

    public bool TryOpenOwnerScope(IssueBase issue, out IDisposable scope)
    {
        scope = null;
        if (issue == null || !objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId) ||
            !ownership.TryGetOwner(issueOwnerId, issue.StringId, out var controllerId) ||
            !playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
        {
            return false;
        }

        scope = new MainHeroSubstitutionScope(hero, party);
        return true;
    }

    public bool IsVisible(QuestBase quest)
    {
        if (!(quest is Quest conquest))
        {
            return true;
        }

        if (questOwners.TryGetValue(conquest.StringId, out var completedOwner))
        {
            return completedOwner == controllerIdProvider.ControllerId;
        }

        var issue = Campaign.Current?.IssueManager == null
            ? null
            : IssueManager.GetIssueOfQuest(conquest);
        return issue != null && IsOwnedBy(issue, Hero.MainHero);
    }

    public bool IsVisible(JournalLogEntry entry)
    {
        if (entry == null || entry._relatedObjectIds == null)
        {
            return true;
        }

        return entry._relatedObjectIds.Any(id =>
            questOwners.TryGetValue(id, out var owner) && owner == controllerIdProvider.ControllerId);
    }

    public void RecordLocalQuest(Quest quest)
    {
        if (quest != null && controllerIdProvider.ControllerId != null)
        {
            questOwners[quest.StringId] = controllerIdProvider.ControllerId;
        }
    }

    public void RecordQuest(Quest quest)
    {
        if (quest == null || !objectManager.TryGetId(quest.QuestGiver, out var issueOwnerId))
        {
            return;
        }

        if (ownership.TryGetOwner(issueOwnerId, IssueManager.GetIssueOfQuest(quest)?.StringId, out var owner))
        {
            questOwners[quest.StringId] = owner;
        }
    }

    internal static void ResetQuestOwners()
    {
        questOwners.Clear();
        questOwnersRestored = false;
    }

    internal static void ResetQuestOwnersIfNotRestored()
    {
        if (!questOwnersRestored)
        {
            questOwners.Clear();
        }
    }

    internal static void SyncQuestOwners(IDataStore dataStore)
    {
        List<TheConquestOfSettlementQuestOwnerSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = questOwners
                .Select(pair => new TheConquestOfSettlementQuestOwnerSaveData(pair.Key, pair.Value))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading)
        {
            return;
        }

        questOwners.Clear();
        questOwnersRestored = true;
        if (saveData == null)
        {
            return;
        }

        foreach (var entry in saveData)
        {
            if (!string.IsNullOrEmpty(entry?.QuestId) && !string.IsNullOrEmpty(entry.ControllerId))
            {
                questOwners[entry.QuestId] = entry.ControllerId;
            }
        }
    }

    private bool IsOwnedBy(IssueBase issue, Hero hero)
    {
        if (hero == null || !objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId) ||
            !ownership.TryGetOwner(issueOwnerId, issue.StringId, out var controllerId) ||
            !playerManager.TryGetPlayer(controllerId, out var player))
        {
            return false;
        }

        return objectManager.TryGetObject<Hero>(player.HeroId, out var owner) && owner == hero;
    }
}
