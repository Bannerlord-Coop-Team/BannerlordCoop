using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using System;

namespace GameInterface.Services.Issues.Framework.Registries;

/// <summary>
/// A client that joins after an issue was accepted never saw the accept, so the server tells it who owns what.
/// The snapshot is the whole truth at join, so it replaces whatever the client remembered.
/// </summary>
internal class IssueOwnershipSnapshotReceiver : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IIssueOwnershipRegistry ownership;

    public IssueOwnershipSnapshotReceiver(IMessageBroker messageBroker, IIssueOwnershipRegistry ownership)
    {
        this.messageBroker = messageBroker;
        this.ownership = ownership;

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

        foreach (var remembered in ownership.GetAll())
        {
            ownership.Remove(remembered.IssueOwnerId);
        }

        foreach (var owner in payload.What.Owners ?? Array.Empty<IssueOwnershipData>())
        {
            ownership.TrySetOwner(owner.IssueOwnerId, owner.IssueId, owner.ControllerId);
        }
    }
}
