using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Caravan Ambush issue replication - creation-time target-settlement capture (same shape
/// as <see cref="SmugglersIssueHandler"/>'s creation half), plus the accept-time party-spawn-gate
/// request/broadcast routing described in <see cref="Patches.CaravanAmbushPartySpawnGatePatch"/>'s doc comment.
///
/// Deliberately does NOT subscribe to any accept-quest/alternative-solution-accept/finalize message of its
/// own - this issue type rides <see cref="Patches.IssueAcceptancePatches"/>/
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>/<see cref="Patches.IssueFinalizedPatches"/>
/// entirely unchanged (see <see cref="Interfaces.GenericAcceptMirrorIssueTypes"/>'s CaravanAmbush entries and
/// <see cref="Interfaces.ICaravanAmbushIssueInterface"/>'s type doc comment for why nothing here needs its own
/// bespoke accept/finalize capture).
/// </summary>
internal class CaravanAmbushIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravanAmbushIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ICaravanAmbushIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface villageToolsInterface;

    public CaravanAmbushIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ICaravanAmbushIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface villageToolsInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.villageToolsInterface = villageToolsInterface;

        messageBroker.Subscribe<CaravanAmbushIssueCreated>(Handle_CaravanAmbushIssueCreated);
        messageBroker.Subscribe<NetworkCaravanAmbushIssueCreated>(Handle_NetworkCaravanAmbushIssueCreated);

        messageBroker.Subscribe<CaravanAmbushAcceptRequested>(Handle_CaravanAmbushAcceptRequested);
        messageBroker.Subscribe<RequestCaravanAmbushAccept>(Handle_RequestCaravanAmbushAccept);
        messageBroker.Subscribe<CaravanAmbushAccepted>(Handle_CaravanAmbushAccepted);
        messageBroker.Subscribe<NetworkCaravanAmbushAccepted>(Handle_NetworkCaravanAmbushAccepted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CaravanAmbushIssueCreated>(Handle_CaravanAmbushIssueCreated);
        messageBroker.Unsubscribe<NetworkCaravanAmbushIssueCreated>(Handle_NetworkCaravanAmbushIssueCreated);

        messageBroker.Unsubscribe<CaravanAmbushAcceptRequested>(Handle_CaravanAmbushAcceptRequested);
        messageBroker.Unsubscribe<RequestCaravanAmbushAccept>(Handle_RequestCaravanAmbushAccept);
        messageBroker.Unsubscribe<CaravanAmbushAccepted>(Handle_CaravanAmbushAccepted);
        messageBroker.Unsubscribe<NetworkCaravanAmbushAccepted>(Handle_NetworkCaravanAmbushAccepted);
    }

    // --- Creation: server rolls once, broadcasts the resolved target settlement ---

    private void Handle_CaravanAmbushIssueCreated(MessagePayload<CaravanAmbushIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureTargetSettlement(issue, out var targetSettlement))
        {
            Logger.Error("Could not capture Caravan Ambush issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(targetSettlement, out var targetSettlementId)) return;

        network.SendAll(new NetworkCaravanAmbushIssueCreated(ownerId, targetSettlementId));
    }

    private void Handle_NetworkCaravanAmbushIssueCreated(MessagePayload<NetworkCaravanAmbushIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<TaleWorlds.CampaignSystem.Settlements.Settlement>(data.TargetSettlementId, out var targetSettlement)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetSettlement);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Accept-time party spawn gate: see Patches.CaravanAmbushPartySpawnGatePatch's doc comment ---

    private void Handle_CaravanAmbushAcceptRequested(MessagePayload<CaravanAmbushAcceptRequested> payload)
    {
        var data = payload.What;
        if (data.Owner == null || !objectManager.TryGetIdWithLogging(data.Owner, out var ownerId)) return;

        // Only ever published on a client (see CaravanAmbushPartySpawnGatePatch.Prefix) - a client's own
        // SendAll only ever reaches the server.
        network.SendAll(new RequestCaravanAmbushAccept(ownerId, data.AccepterMainPartySpeed));
    }

    private void Handle_RequestCaravanAmbushAccept(MessagePayload<RequestCaravanAmbushAccept> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Ensure the server has its own quest mirror before accepting against it - the generic
            // accept-mirror flow (GenericAcceptMirrorIssueTypes, via IssueAcceptancePatches) normally already
            // created this by the time this arrives (the accept-trigger broadcast is sent strictly before the
            // accept-request in the same live conversation), but stay idempotent-safe against any out-of-order
            // arrival.
            villageToolsInterface.MirrorQuestAccepted(owner);

            if (owner.Issue?.IssueQuest is not CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest)
            {
                Logger.Error("No CaravanAmbushIssueQuest mirror available yet for owner {Owner}", data.OwnerId);
                return;
            }

            // Idempotent: a resend must not spawn a second set of parties for the same quest.
            if (issueInterface.TryCaptureAcceptedState(owner, out _, out _, out _)) return;

            if (!issueInterface.CreateReplicatedAccept(owner, data.AccepterMainPartySpeed, out var caravanParty, out var banditParty, out var rewardItems))
            {
                Logger.Error("Failed to create the replicated Caravan Ambush parties for owner {Owner}", data.OwnerId);
                return;
            }

            BroadcastAccepted(owner, caravanParty, banditParty, rewardItems);
        });
    }

    private void Handle_CaravanAmbushAccepted(MessagePayload<CaravanAmbushAccepted> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        BroadcastAccepted(data.Owner, data.CaravanParty, data.BanditParty, data.RewardItems);
    }

    private void BroadcastAccepted(Hero owner, MobileParty caravanParty, MobileParty banditParty, List<ItemObject> rewardItems)
    {
        if (owner == null || caravanParty == null || banditParty == null) return;
        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(caravanParty, out var caravanPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(banditParty, out var banditPartyId)) return;

        var rewardItemIds = new List<string>();
        if (rewardItems != null)
        {
            foreach (var item in rewardItems)
            {
                if (objectManager.TryGetIdWithLogging(item, out var itemId))
                {
                    rewardItemIds.Add(itemId);
                }
            }
        }

        network.SendAll(new NetworkCaravanAmbushAccepted(ownerId, caravanPartyId, banditPartyId, rewardItemIds));
    }

    private void Handle_NetworkCaravanAmbushAccepted(MessagePayload<NetworkCaravanAmbushAccepted> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.CaravanPartyId, out var caravanParty)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.BanditPartyId, out var banditParty)) return;

            // Ensure this peer has its own quest mirror too (same idempotent-safety reasoning as the server
            // handler above).
            villageToolsInterface.MirrorQuestAccepted(owner);

            List<ItemObject> rewardItems = null;
            if (data.RewardItemIds is { Count: > 0 })
            {
                rewardItems = new List<ItemObject>();
                foreach (var itemId in data.RewardItemIds)
                {
                    if (objectManager.TryGetObjectWithLogging<ItemObject>(itemId, out var item))
                    {
                        rewardItems.Add(item);
                    }
                }
            }

            issueInterface.ForceAcceptedState(owner, caravanParty, banditParty, rewardItems);
        });
    }
}
