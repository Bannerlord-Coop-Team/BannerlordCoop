using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Interface;

internal interface IIssueConversationGuard
{
    // Runs a removal of the giver's issue or its quest now, or once this player's conversation with the giver ends
    void RunWhenNotTalkingTo(Action removal, string issueOwnerId);

    // Runs now, or behind work still waiting for any of these givers, so their messages apply in the order they came
    void RunInOrder(Action apply, params string[] issueOwnerIds);

    void OnConversationEnded();

    // Tells the player the issue is gone, and closes their conversation with the giver after the accept line
    void EndUnavailableAccept(Hero issueOwner);
}
