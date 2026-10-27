using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Conversation;

/// <summary>
/// Keeps a player's open conversation with an issue giver answerable. The options on screen point at dialog lines
/// by index, and the accept line's answer comes from the quest, so removing the issue or its quest under them leaves
/// the chosen line without an answer.
/// </summary>
internal sealed class IssueConversationGuard : IIssueConversationGuard
{
    private readonly IObjectManager objectManager;
    private readonly List<(string IssueOwnerId, Action Removal)> deferred = new();

    public IssueConversationGuard(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void RunWhenNotTalkingTo(string issueOwnerId, Action removal)
    {
        if (IsTalkingTo(issueOwnerId))
        {
            deferred.Add((issueOwnerId, removal));
            return;
        }

        RunDeferred(issueOwnerId);
        removal();
    }

    public void RunDeferred(string issueOwnerId) => Run(entry => entry.IssueOwnerId == issueOwnerId);

    public void OnConversationEnded() => Run(entry => !IsTalkingTo(entry.IssueOwnerId));

    public void EndUnavailableAccept()
    {
        MBInformationManager.AddQuickInformation(new TextObject(
            "{=str_coop_issue_quest_unavailable}This issue is no longer available as a quest."));

        // Vanilla ends the conversation after a player line whose output is close_window
        var conversation = Campaign.Current?.ConversationManager;
        if (conversation?.IsConversationInProgress == true)
        {
            conversation.ActiveToken = conversation.GetStateIndex("close_window");
        }
    }

    private void Run(Predicate<(string IssueOwnerId, Action Removal)> isReady)
    {
        var ready = deferred.FindAll(isReady);
        deferred.RemoveAll(isReady);

        foreach (var entry in ready)
        {
            GameThread.RunSafe(entry.Removal, context: nameof(IssueConversationGuard));
        }
    }

    private bool IsTalkingTo(string issueOwnerId)
    {
        var conversation = Campaign.Current?.ConversationManager;
        return conversation?.IsConversationInProgress == true &&
            objectManager.TryGetId(conversation.OneToOneConversationHero, out var heroId) && heroId == issueOwnerId;
    }
}
