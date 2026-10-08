using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;

namespace Coop.Core.Server.Services.Issues.Handlers;

/// <summary>
/// A player who joins after an issue was accepted loads it in the save without seeing the accept, so it is
/// told who owns each issue when it enters the campaign.
/// </summary>
internal class IssueOwnershipSnapshotHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IIssueOwnershipRegistry ownership;

    public IssueOwnershipSnapshotHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IIssueOwnershipRegistry ownership)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.ownership = ownership;

        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var owners = ownership.GetAll();
        if (owners.Length == 0)
        {
            return;
        }

        network.Send(payload.What.playerId, new NetworkIssueOwnershipSnapshot(owners));
    }
}
