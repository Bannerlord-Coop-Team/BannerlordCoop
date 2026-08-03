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
/// Server-authoritative Nearby Bandit Base issue CREATION replication only - acceptance (both quest-solution and
/// alternative-solution) rides the fully generic mirror (see <c>GenericAcceptMirrorIssueTypes</c>) unchanged, and
/// removal rides the existing generic finalize choke point (see
/// <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s doc comment for why per-type handlers deliberately
/// don't add their own parallel finalize subscription).
/// </summary>
internal class NearbyBanditBaseIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NearbyBanditBaseIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly INearbyBanditBaseIssueInterface issueInterface;

    public NearbyBanditBaseIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        INearbyBanditBaseIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<NearbyBanditBaseIssueCreated>(Handle_NearbyBanditBaseIssueCreated);
        messageBroker.Subscribe<NetworkNearbyBanditBaseIssueCreated>(Handle_NetworkNearbyBanditBaseIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NearbyBanditBaseIssueCreated>(Handle_NearbyBanditBaseIssueCreated);
        messageBroker.Unsubscribe<NetworkNearbyBanditBaseIssueCreated>(Handle_NetworkNearbyBanditBaseIssueCreated);
    }

    private void Handle_NearbyBanditBaseIssueCreated(MessagePayload<NearbyBanditBaseIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var targetHideout))
        {
            Logger.Error("Could not capture Nearby Bandit Base issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(targetHideout, out var targetHideoutId)) return;

        network.SendAll(new NetworkNearbyBanditBaseIssueCreated(ownerId, targetHideoutId));
    }

    private void Handle_NetworkNearbyBanditBaseIssueCreated(MessagePayload<NetworkNearbyBanditBaseIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.TargetHideoutId, out var targetHideout)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetHideout);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
