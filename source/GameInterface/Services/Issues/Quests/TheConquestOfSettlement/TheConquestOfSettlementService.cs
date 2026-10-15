using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Concurrent;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

internal interface ITheConquestOfSettlementService
{
    bool TryOpenOwnerScope(IssueBase issue, out IDisposable scope);
    bool HasQuest(Hero hero);
    bool IsVisible(QuestBase quest);
    bool IsVisible(JournalLogEntry entry);
    void RecordLocalQuest(Quest quest);
    void RecordQuest(Quest quest);
}

internal sealed class TheConquestOfSettlementService : ITheConquestOfSettlementService
{
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ConcurrentDictionary<string, string> questOwners = new();

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

    public bool HasQuest(Hero hero)
    {
        if (hero == null || Campaign.Current?.IssueManager == null)
        {
            return false;
        }

        return Campaign.Current.IssueManager.Issues.Values
            .OfType<Issue>()
            .Any(issue => issue.IsSolvingWithQuest && IsOwnedBy(issue, hero));
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
