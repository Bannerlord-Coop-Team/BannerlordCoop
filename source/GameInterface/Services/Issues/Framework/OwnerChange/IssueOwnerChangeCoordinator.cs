using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.OwnerChange;

/// <summary>
/// The server hands a dead notable's issue to a relative, so every client moves its copy and its ownership mirror
/// to the new owner, otherwise later messages name a hero the client has no issue on.
/// </summary>
internal class IssueOwnerChangeCoordinator : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueConversationGuard conversationGuard;

    public IssueOwnerChangeCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IIssueConversationGuard conversationGuard)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.conversationGuard = conversationGuard;

        messageBroker.Subscribe<IssueOwnerChanged>(Handle_IssueOwnerChanged);
        messageBroker.Subscribe<NetworkIssueOwnerChanged>(Handle_NetworkIssueOwnerChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IssueOwnerChanged>(Handle_IssueOwnerChanged);
        messageBroker.Unsubscribe<NetworkIssueOwnerChanged>(Handle_NetworkIssueOwnerChanged);
    }

    // Server only.
    internal void Handle_IssueOwnerChanged(MessagePayload<IssueOwnerChanged> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issue = payload.What.Issue;

        if (!objectManager.TryGetIdWithLogging(payload.What.OldOwner, out var oldOwnerId) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var newOwnerId))
        {
            return;
        }

        MoveOwnership(oldOwnerId, newOwnerId, issue.StringId);

        network.SendAll(new NetworkIssueOwnerChanged(oldOwnerId, newOwnerId, issue.StringId));
    }

    // Client only.
    internal void Handle_NetworkIssueOwnerChanged(MessagePayload<NetworkIssueOwnerChanged> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        // Work a conversation holds back can name the old owner, and the new owner may still hold an ended issue
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            MoveOwnership(data.OldOwnerId, data.NewOwnerId, data.IssueId);

            if (!issueResolver.TryResolve(data.OldOwnerId, data.IssueId, out _, out var issue, out _))
            {
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.NewOwnerId, out var newOwner))
            {
                return;
            }

            using (new AllowedThread())
            {
                Campaign.Current.IssueManager.ChangeIssueOwner(issue, newOwner);
            }
        }, data.OldOwnerId, data.NewOwnerId), context: nameof(IssueOwnerChangeCoordinator));
    }

    private void MoveOwnership(string oldOwnerId, string newOwnerId, string issueId)
    {
        if (!ownership.TryGetOwner(oldOwnerId, issueId, out var controllerId))
        {
            return;
        }

        ownership.Remove(oldOwnerId);
        ownership.TrySetOwner(newOwnerId, issueId, controllerId);
    }
}
