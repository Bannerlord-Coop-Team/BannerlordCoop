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
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative CREATION handling for Deliver the Herd (see
/// <see cref="IHeadmanNeedsToDeliverAHerdIssueInterface"/>'s doc comment for why this is the only bespoke
/// piece this type needs). Acceptance (quest AND alternative solution), accept-race arbitration, real
/// ownership recording, and finalize/removal all ride the fully generic
/// <see cref="VillageNeedsToolsIssueHandler"/> + <see cref="VillageNeedsToolsIssueInterface"/> +
/// <see cref="VillageNeedsToolsIssueOwnership"/> machinery unchanged - this type is registered into
/// <see cref="GenericAcceptMirrorIssueTypes"/>'s two eligible sets instead of growing its own parallel
/// accept/reject message set.
/// </summary>
internal class HeadmanNeedsToDeliverAHerdIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeadmanNeedsToDeliverAHerdIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IHeadmanNeedsToDeliverAHerdIssueInterface issueInterface;

    public HeadmanNeedsToDeliverAHerdIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IHeadmanNeedsToDeliverAHerdIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<HeadmanNeedsToDeliverAHerdIssueCreated>(Handle_HeadmanNeedsToDeliverAHerdIssueCreated);
        messageBroker.Subscribe<NetworkHeadmanNeedsToDeliverAHerdIssueCreated>(Handle_NetworkHeadmanNeedsToDeliverAHerdIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeadmanNeedsToDeliverAHerdIssueCreated>(Handle_HeadmanNeedsToDeliverAHerdIssueCreated);
        messageBroker.Unsubscribe<NetworkHeadmanNeedsToDeliverAHerdIssueCreated>(Handle_NetworkHeadmanNeedsToDeliverAHerdIssueCreated);
    }

    private void Handle_HeadmanNeedsToDeliverAHerdIssueCreated(MessagePayload<HeadmanNeedsToDeliverAHerdIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var targetSettlement, out var targetHero, out var herdTypeToDeliver))
        {
            Logger.Error("Could not capture Deliver the Herd issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(targetSettlement, out var targetSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(targetHero, out var targetHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(herdTypeToDeliver, out var herdTypeToDeliverId)) return;

        network.SendAll(new NetworkHeadmanNeedsToDeliverAHerdIssueCreated(
            ownerId, targetSettlementId, targetHeroId, herdTypeToDeliverId));
    }

    private void Handle_NetworkHeadmanNeedsToDeliverAHerdIssueCreated(MessagePayload<NetworkHeadmanNeedsToDeliverAHerdIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.TargetSettlementId, out var targetSettlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.TargetHeroId, out var targetHero)) return;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.HerdTypeToDeliverId, out var herdTypeToDeliver)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetSettlement, targetHero, herdTypeToDeliver);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
