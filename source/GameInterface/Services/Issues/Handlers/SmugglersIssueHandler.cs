using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Smugglers issue replication - creation-time target/origin-settlement capture (same
/// shape as <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s creation half), plus the party-spawn-gate
/// request/broadcast routing described in <see cref="Patches.SmugglersPartySpawnGatePatch"/>'s doc comment.
///
/// Deliberately does NOT subscribe to any accept-quest/alternative-solution-accept/finalize message of its
/// own - this issue type rides <see cref="Patches.IssueAcceptancePatches"/>/
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>/<see cref="Patches.IssueFinalizedPatches"/>
/// entirely unchanged (see <see cref="Interfaces.GenericAcceptMirrorIssueTypes"/>'s Smugglers entries and
/// <see cref="Interfaces.ISmugglersIssueInterface"/>'s type doc comment for why nothing here needs its own
/// bespoke accept/finalize capture).
/// </summary>
internal class SmugglersIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SmugglersIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISmugglersIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface villageToolsInterface;

    public SmugglersIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISmugglersIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface villageToolsInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.villageToolsInterface = villageToolsInterface;

        messageBroker.Subscribe<SmugglersIssueCreated>(Handle_SmugglersIssueCreated);
        messageBroker.Subscribe<NetworkSmugglersIssueCreated>(Handle_NetworkSmugglersIssueCreated);

        messageBroker.Subscribe<SmugglersPartySpawnRequested>(Handle_SmugglersPartySpawnRequested);
        messageBroker.Subscribe<RequestSmugglerPartySpawn>(Handle_RequestSmugglerPartySpawn);
        messageBroker.Subscribe<SmugglersPartySpawned>(Handle_SmugglersPartySpawned);
        messageBroker.Subscribe<NetworkSmugglersPartySpawned>(Handle_NetworkSmugglersPartySpawned);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SmugglersIssueCreated>(Handle_SmugglersIssueCreated);
        messageBroker.Unsubscribe<NetworkSmugglersIssueCreated>(Handle_NetworkSmugglersIssueCreated);

        messageBroker.Unsubscribe<SmugglersPartySpawnRequested>(Handle_SmugglersPartySpawnRequested);
        messageBroker.Unsubscribe<RequestSmugglerPartySpawn>(Handle_RequestSmugglerPartySpawn);
        messageBroker.Unsubscribe<SmugglersPartySpawned>(Handle_SmugglersPartySpawned);
        messageBroker.Unsubscribe<NetworkSmugglersPartySpawned>(Handle_NetworkSmugglersPartySpawned);
    }

    // --- Creation: server rolls once, broadcasts the resolved target/origin settlement pair ---

    private void Handle_SmugglersIssueCreated(MessagePayload<SmugglersIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var targetSettlement, out var originSettlement))
        {
            Logger.Error("Could not capture Smugglers issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(targetSettlement, out var targetSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(originSettlement, out var originSettlementId)) return;

        network.SendAll(new NetworkSmugglersIssueCreated(ownerId, targetSettlementId, originSettlementId));
    }

    private void Handle_NetworkSmugglersIssueCreated(MessagePayload<NetworkSmugglersIssueCreated> payload)
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
            if (!objectManager.TryGetObjectWithLogging<TaleWorlds.CampaignSystem.Settlements.Settlement>(data.OriginSettlementId, out var originSettlement)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetSettlement, originSettlement);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Party spawn gate: see Patches.SmugglersPartySpawnGatePatch's doc comment for the full mechanism ---

    private void Handle_SmugglersPartySpawnRequested(MessagePayload<SmugglersPartySpawnRequested> payload)
    {
        var data = payload.What;
        if (data.Owner == null || !objectManager.TryGetIdWithLogging(data.Owner, out var ownerId)) return;

        // Only ever published on a client (see SmugglersPartySpawnGatePatch.Prefix) - a client's own SendAll
        // only ever reaches the server.
        network.SendAll(new RequestSmugglerPartySpawn(ownerId, data.DesiredMenCount, data.CustomPartyBaseSpeed));
    }

    private void Handle_RequestSmugglerPartySpawn(MessagePayload<RequestSmugglerPartySpawn> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Ensure the server has its own quest mirror before creating the party against it - the generic
            // accept-mirror flow (GenericAcceptMirrorIssueTypes, via IssueAcceptancePatch) normally already
            // created this by the time this arrives (the accept-trigger broadcast is sent strictly before the
            // party-spawn request in the same live conversation), but stay idempotent-safe against any
            // out-of-order arrival.
            villageToolsInterface.MirrorQuestAccepted(owner);

            if (owner.Issue?.IssueQuest is not SmugglersIssueBehavior.SmugglersIssueQuest) return;

            // Idempotent: a resend must not spawn a second party for the same quest.
            if (issueInterface.TryCaptureSmugglerParty(owner, out _)) return;

            var party = issueInterface.CreateReplicatedSmugglerParty(owner, data.DesiredMenCount, data.CustomPartyBaseSpeed);
            if (party == null)
            {
                Logger.Error("Failed to create the replicated smuggler party for owner {Owner}", data.OwnerId);
                return;
            }

            BroadcastPartySpawned(owner, party);
        });
    }

    private void Handle_SmugglersPartySpawned(MessagePayload<SmugglersPartySpawned> payload)
    {
        if (ModInformation.IsClient) return;

        BroadcastPartySpawned(payload.What.Owner, payload.What.Party);
    }

    private void BroadcastPartySpawned(Hero owner, MobileParty party)
    {
        if (owner == null || party == null) return;
        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(party, out var partyId)) return;

        network.SendAll(new NetworkSmugglersPartySpawned(ownerId, partyId));
    }

    private void Handle_NetworkSmugglersPartySpawned(MessagePayload<NetworkSmugglersPartySpawned> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.PartyId, out var party)) return;

            // Ensure this peer has its own quest mirror too (same idempotent-safety reasoning as the server
            // handler above).
            villageToolsInterface.MirrorQuestAccepted(owner);

            issueInterface.ForceSmugglerParty(owner, party);
        });
    }
}
