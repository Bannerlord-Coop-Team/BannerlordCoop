using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Captured by Bounty Hunters issue CREATION replication only - acceptance rides the
/// fully generic mirror (see <c>GenericAcceptMirrorIssueTypes</c>) unchanged, and removal rides the existing
/// generic finalize choke point (see <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s doc comment for
/// why per-type handlers deliberately don't add their own parallel finalize subscription).
/// </summary>
internal class CapturedByBountyHuntersIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CapturedByBountyHuntersIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ICapturedByBountyHuntersIssueInterface issueInterface;

    public CapturedByBountyHuntersIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ICapturedByBountyHuntersIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<CapturedByBountyHuntersIssueCreated>(Handle_CapturedByBountyHuntersIssueCreated);
        messageBroker.Subscribe<NetworkCapturedByBountyHuntersIssueCreated>(Handle_NetworkCapturedByBountyHuntersIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CapturedByBountyHuntersIssueCreated>(Handle_CapturedByBountyHuntersIssueCreated);
        messageBroker.Unsubscribe<NetworkCapturedByBountyHuntersIssueCreated>(Handle_NetworkCapturedByBountyHuntersIssueCreated);
    }

    private void Handle_CapturedByBountyHuntersIssueCreated(MessagePayload<CapturedByBountyHuntersIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var hideout))
        {
            Logger.Error("Could not capture Captured by Bounty Hunters issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(hideout, out var hideoutId)) return;

        network.SendAll(new NetworkCapturedByBountyHuntersIssueCreated(ownerId, hideoutId));
    }

    private void Handle_NetworkCapturedByBountyHuntersIssueCreated(MessagePayload<NetworkCapturedByBountyHuntersIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.HideoutId, out var hideout)) return;

            var replicated = issueInterface.ConstructReplicated(owner, hideout);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
