using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Conversation;

/// <summary>
/// Keeps a player's open conversation with an issue giver answerable. The options on screen point at dialog lines
/// by index, and the accept line's answer comes from the quest, so removing the issue or its quest under them leaves
/// the chosen line without an answer. Later messages for that giver wait behind the removal, so they keep their order.
/// </summary>
internal sealed class IssueConversationGuard : IIssueConversationGuard
{
    private readonly IObjectManager objectManager;
    private readonly List<(string[] IssueOwnerIds, Action Apply)> waiting = new();

    public IssueConversationGuard(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void RunWhenNotTalkingTo(Action removal, string issueOwnerId)
    {
        if (IsTalkingTo(issueOwnerId))
        {
            waiting.Add((new[] { issueOwnerId }, removal));
            return;
        }

        RunInOrder(removal, issueOwnerId);
    }

    public void RunInOrder(Action apply, params string[] issueOwnerIds)
    {
        if (waiting.Any(entry => entry.IssueOwnerIds.Any(issueOwnerIds.Contains)))
        {
            waiting.Add((issueOwnerIds, apply));
            return;
        }

        apply();
    }

    public void OnConversationEnded()
    {
        if (waiting.Count == 0) return;

        var stillWaiting = new HashSet<string>();
        var index = 0;

        while (index < waiting.Count)
        {
            var entry = waiting[index];

            // Anything behind work that has to keep waiting keeps waiting too, so each giver's order holds
            if (entry.IssueOwnerIds.Any(id => stillWaiting.Contains(id) || IsTalkingTo(id)))
            {
                stillWaiting.UnionWith(entry.IssueOwnerIds);
                index++;
                continue;
            }

            waiting.RemoveAt(index);
            GameThread.RunSafe(entry.Apply, context: nameof(IssueConversationGuard));
        }
    }

    public void EndUnavailableAccept(Hero issueOwner)
    {
        MBInformationManager.AddQuickInformation(new TextObject(
            "{=str_coop_issue_quest_unavailable}This issue is no longer available as a quest."));

        // Vanilla ends the conversation after a player line whose output is close_window
        var conversation = Campaign.Current?.ConversationManager;
        if (conversation?.IsConversationInProgress == true && conversation.OneToOneConversationHero == issueOwner)
        {
            conversation.ActiveToken = conversation.GetStateIndex("close_window");
        }
    }

    private bool IsTalkingTo(string issueOwnerId)
    {
        var conversation = Campaign.Current?.ConversationManager;
        return conversation?.IsConversationInProgress == true &&
            objectManager.TryGetId(conversation.OneToOneConversationHero, out var heroId) && heroId == issueOwnerId;
    }
}
