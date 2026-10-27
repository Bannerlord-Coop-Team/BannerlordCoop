using System;

namespace GameInterface.Services.Issues.Framework.Interface;

internal interface IIssueConversationGuard
{
    // Runs the removal of the giver's issue or quest now, or once this player's conversation with the giver ends
    void RunWhenNotTalkingTo(string issueOwnerId, Action removal);

    // Runs the giver's deferred removals now, before a later message changes the giver's issue
    void RunDeferred(string issueOwnerId);

    void OnConversationEnded();

    // Tells the player the issue is gone and closes the conversation after their accept line
    void EndUnavailableAccept();
}
