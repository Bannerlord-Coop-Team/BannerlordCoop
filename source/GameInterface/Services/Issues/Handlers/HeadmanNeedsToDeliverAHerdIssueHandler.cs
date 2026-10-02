using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

internal class HeadmanNeedsToDeliverAHerdIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeadmanNeedsToDeliverAHerdIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IHeadmanNeedsToDeliverAHerdIssueInterface issueInterface;
    private readonly IIssueGenerationRegistry generationRegistry;

    public HeadmanNeedsToDeliverAHerdIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IHeadmanNeedsToDeliverAHerdIssueInterface issueInterface,
        IIssueGenerationRegistry generationRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.generationRegistry = generationRegistry;

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
            ownerId, targetSettlementId, targetHeroId, herdTypeToDeliverId, generationRegistry.Bump(issue.IssueOwner),
            issue.StringId, issue.IssueCreationTime.NumTicks, issue.IssueDueTime.NumTicks));
    }

    private void Handle_NetworkHeadmanNeedsToDeliverAHerdIssueCreated(MessagePayload<NetworkHeadmanNeedsToDeliverAHerdIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (generationRegistry.TryGetGeneration(owner, out var currentGeneration) && currentGeneration >= data.Generation) return;
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.TargetSettlementId, out var targetSettlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.TargetHeroId, out var targetHero)) return;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.HerdTypeToDeliverId, out var herdTypeToDeliver)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetSettlement, targetHero, herdTypeToDeliver);

            issueInterface.RegisterReplicated(owner, replicated, data.IssueId,
                new CampaignTime(data.CreationTimeTicks), new CampaignTime(data.DueTimeTicks));
            generationRegistry.SetGeneration(owner, data.Generation);
        });
    }
}
