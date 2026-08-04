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
/// Server-authoritative Escort Merchant Caravan issue replication - creation-time <c>_companionRewardRandom</c>
/// capture (same shape as <see cref="LordNeedsHorsesIssueHandler"/>'s creation half), and the accept-time
/// caravan-party spawn-gate request/broadcast routing (see
/// <see cref="Patches.EscortMerchantCaravanPartySpawnGatePatch"/>'s doc comment).
///
/// Deliberately does NOT subscribe to any accept-quest/alternative-solution-accept/finalize message of its own -
/// this issue type rides <see cref="Patches.IssueAcceptancePatches"/>/
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>/<see cref="Patches.IssueFinalizedPatches"/>
/// entirely unchanged (see <see cref="GenericAcceptMirrorIssueTypes"/>'s EscortMerchantCaravan entries and
/// <see cref="IEscortMerchantCaravanIssueInterface"/>'s type doc comment for why nothing here needs its own
/// bespoke accept/finalize capture). This ALSO means the generic accept-mirror mechanism
/// (<see cref="Patches.IssueAcceptancePatches"/> + <see cref="VillageNeedsToolsIssueHandler"/>'s own
/// <see cref="VillageNeedsToolsIssueOwnership"/>.SetOwner call) is the complete answer to the design doc's own
/// flagged-open "accept handler + persistence" prerequisite (doc/EscortMerchantCaravan_Design_v2.md §3.4/§7
/// item 1) - no bespoke accept handler or persistence hook is needed here at all, since this quest type only
/// needs adding to the two existing generic eligibility sets plus the existing generic
/// <c>NewIssueTypesAlternativeSolutionPatches</c> HourlyTick registration.
/// </summary>
internal class EscortMerchantCaravanIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<EscortMerchantCaravanIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IEscortMerchantCaravanIssueInterface issueInterface;

    public EscortMerchantCaravanIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IEscortMerchantCaravanIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<EscortMerchantCaravanIssueCreated>(Handle_EscortMerchantCaravanIssueCreated);
        messageBroker.Subscribe<NetworkEscortMerchantCaravanIssueCreated>(Handle_NetworkEscortMerchantCaravanIssueCreated);

        messageBroker.Subscribe<EscortMerchantCaravanPartySpawnRequested>(Handle_PartySpawnRequested);
        messageBroker.Subscribe<RequestEscortMerchantCaravanPartySpawn>(Handle_RequestPartySpawn);
        messageBroker.Subscribe<EscortMerchantCaravanPartySpawned>(Handle_PartySpawned);
        messageBroker.Subscribe<NetworkEscortMerchantCaravanPartySpawned>(Handle_NetworkPartySpawned);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EscortMerchantCaravanIssueCreated>(Handle_EscortMerchantCaravanIssueCreated);
        messageBroker.Unsubscribe<NetworkEscortMerchantCaravanIssueCreated>(Handle_NetworkEscortMerchantCaravanIssueCreated);

        messageBroker.Unsubscribe<EscortMerchantCaravanPartySpawnRequested>(Handle_PartySpawnRequested);
        messageBroker.Unsubscribe<RequestEscortMerchantCaravanPartySpawn>(Handle_RequestPartySpawn);
        messageBroker.Unsubscribe<EscortMerchantCaravanPartySpawned>(Handle_PartySpawned);
        messageBroker.Unsubscribe<NetworkEscortMerchantCaravanPartySpawned>(Handle_NetworkPartySpawned);
    }

    // --- Creation: server rolls once, broadcasts the resolved _companionRewardRandom ---

    private void Handle_EscortMerchantCaravanIssueCreated(MessagePayload<EscortMerchantCaravanIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureCompanionRewardRandom(issue, out var companionRewardRandom))
        {
            Logger.Error("Could not capture Escort Merchant Caravan issue fields for owner {Owner}", ownerId);
            return;
        }

        network.SendAll(new NetworkEscortMerchantCaravanIssueCreated(ownerId, companionRewardRandom));
    }

    private void Handle_NetworkEscortMerchantCaravanIssueCreated(MessagePayload<NetworkEscortMerchantCaravanIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            var replicated = issueInterface.ConstructReplicated(owner, data.CompanionRewardRandom);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Caravan-party spawn gate: see Patches.EscortMerchantCaravanPartySpawnGatePatch's doc comment ---

    private void Handle_PartySpawnRequested(MessagePayload<EscortMerchantCaravanPartySpawnRequested> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        // Only ever published on a client (see the Patch's Prefix) - a client's own SendAll only ever reaches
        // the server.
        network.SendAll(new RequestEscortMerchantCaravanPartySpawn(ownerId));
    }

    private void Handle_RequestPartySpawn(MessagePayload<RequestEscortMerchantCaravanPartySpawn> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (owner.Issue?.IssueQuest is not EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest)
            {
                Logger.Error("No EscortMerchantCaravanIssueQuest mirror available yet for owner {Owner}", data.OwnerId);
                return;
            }

            // Idempotent (SpawnCaravanOnServer itself already no-ops if the party exists). The broadcast itself
            // happens via Patches.EscortMerchantCaravanPartySpawnGatePatch's own Postfix on SpawnCaravan(),
            // re-triggered by this reflective invoke - see IEscortMerchantCaravanIssueInterface's type doc
            // comment for why no separate publish is needed here.
            var party = issueInterface.SpawnCaravanOnServer(owner);
            if (party == null)
            {
                Logger.Error("Failed to create the replicated Escort Merchant Caravan party for owner {Owner}", data.OwnerId);
            }
        });
    }

    private void Handle_PartySpawned(MessagePayload<EscortMerchantCaravanPartySpawned> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        if (data.Owner == null || data.CaravanParty == null) return;
        if (!objectManager.TryGetIdWithLogging(data.Owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.CaravanParty, out var caravanPartyId)) return;

        network.SendAll(new NetworkEscortMerchantCaravanPartySpawned(ownerId, caravanPartyId));
    }

    private void Handle_NetworkPartySpawned(MessagePayload<NetworkEscortMerchantCaravanPartySpawned> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.CaravanPartyId, out var caravanParty)) return;

            issueInterface.ForceCaravanParty(owner, caravanParty);
        });
    }
}
