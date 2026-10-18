using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryHeirSelectionHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IIssueOwnerResolver ownerResolver;

    public RaidAnEnemyTerritoryHeirSelectionHandler(IMessageBroker messageBroker, IIssueOwnerResolver ownerResolver)
    {
        this.messageBroker = messageBroker;
        this.ownerResolver = ownerResolver;
        messageBroker.Subscribe<ChangePlayerCharacterAfterHeirSelection>(HandlePlayerChange);
    }

    public void Dispose() => messageBroker.Unsubscribe<ChangePlayerCharacterAfterHeirSelection>(HandlePlayerChange);

    private void HandlePlayerChange(MessagePayload<ChangePlayerCharacterAfterHeirSelection> payload)
    {
        if (ModInformation.IsClient || payload.What.OriginalHero == null || Campaign.Current?.IssueManager == null)
        {
            return;
        }

        var issues = new List<IssueBase>();
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            issues.Add(issue);
        }

        foreach (var issue in issues)
        {
            if (issue.IssueQuest is Quest quest && quest.IsOngoing &&
                ownerResolver.TryResolveOwnerHero(issue, out var ownerHero) && ownerHero == payload.What.OriginalHero)
            {
                quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan."));
            }
        }
    }
}
