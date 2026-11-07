using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Linq;

namespace GameInterface.Services.Issues.Framework.Registries;

/// <summary>
/// A client that joins after an issue was accepted never saw the accept, so the server tells it who owns what.
/// The snapshot is the whole truth at join, so it replaces whatever the client remembered.
/// </summary>
internal class IssueOwnershipSnapshotReceiver : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueResolver resolver;
    private readonly IControllerIdProvider controller;

    public IssueOwnershipSnapshotReceiver(IMessageBroker messageBroker, IIssueOwnershipRegistry ownership,
        IIssueResolver resolver, IControllerIdProvider controller)
    {
        this.messageBroker = messageBroker;
        this.ownership = ownership;
        this.resolver = resolver;
        this.controller = controller;

        messageBroker.Subscribe<NetworkIssueOwnershipSnapshot>(Handle_NetworkIssueOwnershipSnapshot);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkIssueOwnershipSnapshot>(Handle_NetworkIssueOwnershipSnapshot);
    }

    internal void Handle_NetworkIssueOwnershipSnapshot(MessagePayload<NetworkIssueOwnershipSnapshot> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        GameThread.RunSafe(() => Apply(payload.What), context: nameof(IssueOwnershipSnapshotReceiver));
    }

    private void Apply(NetworkIssueOwnershipSnapshot snapshot)
    {
        var owners = snapshot.Owners ?? Array.Empty<IssueOwnershipData>();
        foreach (var remembered in ownership.GetAll()
                     .Where(remembered => !owners.Any(owner =>
                         owner.IssueOwnerId == remembered.IssueOwnerId && owner.IssueId == remembered.IssueId)))
        {
            // The client may have missed the server cancellation while disconnected. Replay a local
            // cancel so its quest and tracked objects do not survive a snapshot that removed it.
            messageBroker.Publish(this, new NetworkIssueFinalized(
                remembered.IssueOwnerId, remembered.IssueId, IssueOutcome.QuestCancel));
        }

        // Clear remembered controllers before the authoritative snapshot, since accept is first-wins.
        foreach (var remembered in ownership.GetAll())
            ownership.Remove(remembered.IssueOwnerId);

        foreach (var owner in owners)
        {
            ownership.TrySetOwner(owner.IssueOwnerId, owner.IssueId, owner.ControllerId);
            if (resolver.TryResolve(owner.IssueOwnerId, owner.IssueId, out _, out var issue, out _))
                issue.IsTriedToSolveBefore = owner.ControllerId == controller.ControllerId;
        }
    }
}
