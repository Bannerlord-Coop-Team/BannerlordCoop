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
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Merchant Army of Poachers issue replication - creation-time quest-village capture (same
/// shape as <see cref="CaravanAmbushIssueHandler"/>'s creation half), the accept-time poachers-party spawn-gate
/// request/broadcast routing (see <see cref="Patches.MerchantArmyOfPoachersPartySpawnGatePatch"/>'s doc
/// comment), and the battle-start approval request/broadcast routing (see
/// <see cref="Patches.MerchantArmyOfPoachersBattleStartApprovalPatches"/>'s doc comment).
///
/// Deliberately does NOT subscribe to any accept-quest/alternative-solution-accept/finalize message of its own -
/// this issue type rides <see cref="Patches.IssueAcceptancePatches"/>/
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>/<see cref="Patches.IssueFinalizedPatches"/>
/// entirely unchanged (see <see cref="GenericAcceptMirrorIssueTypes"/>'s MerchantArmyOfPoachers entries and
/// <see cref="IMerchantArmyOfPoachersIssueInterface"/>'s type doc comment for why nothing here needs its own
/// bespoke accept/finalize capture).
/// </summary>
internal class MerchantArmyOfPoachersIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MerchantArmyOfPoachersIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMerchantArmyOfPoachersIssueInterface issueInterface;

    public MerchantArmyOfPoachersIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMerchantArmyOfPoachersIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<MerchantArmyOfPoachersIssueCreated>(Handle_MerchantArmyOfPoachersIssueCreated);
        messageBroker.Subscribe<NetworkMerchantArmyOfPoachersIssueCreated>(Handle_NetworkMerchantArmyOfPoachersIssueCreated);

        messageBroker.Subscribe<MerchantArmyOfPoachersPartySpawnRequested>(Handle_PartySpawnRequested);
        messageBroker.Subscribe<RequestMerchantArmyOfPoachersPartySpawn>(Handle_RequestPartySpawn);
        messageBroker.Subscribe<MerchantArmyOfPoachersPartySpawned>(Handle_PartySpawned);
        messageBroker.Subscribe<NetworkMerchantArmyOfPoachersPartySpawned>(Handle_NetworkPartySpawned);

        messageBroker.Subscribe<MerchantArmyOfPoachersBattleStartRequested>(Handle_BattleStartRequested);
        messageBroker.Subscribe<NetworkMerchantArmyOfPoachersBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Subscribe<MerchantArmyOfPoachersBattleApproved>(Handle_BattleApproved);
        messageBroker.Subscribe<NetworkMerchantArmyOfPoachersBattleApproved>(Handle_NetworkBattleApproved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MerchantArmyOfPoachersIssueCreated>(Handle_MerchantArmyOfPoachersIssueCreated);
        messageBroker.Unsubscribe<NetworkMerchantArmyOfPoachersIssueCreated>(Handle_NetworkMerchantArmyOfPoachersIssueCreated);

        messageBroker.Unsubscribe<MerchantArmyOfPoachersPartySpawnRequested>(Handle_PartySpawnRequested);
        messageBroker.Unsubscribe<RequestMerchantArmyOfPoachersPartySpawn>(Handle_RequestPartySpawn);
        messageBroker.Unsubscribe<MerchantArmyOfPoachersPartySpawned>(Handle_PartySpawned);
        messageBroker.Unsubscribe<NetworkMerchantArmyOfPoachersPartySpawned>(Handle_NetworkPartySpawned);

        messageBroker.Unsubscribe<MerchantArmyOfPoachersBattleStartRequested>(Handle_BattleStartRequested);
        messageBroker.Unsubscribe<NetworkMerchantArmyOfPoachersBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Unsubscribe<MerchantArmyOfPoachersBattleApproved>(Handle_BattleApproved);
        messageBroker.Unsubscribe<NetworkMerchantArmyOfPoachersBattleApproved>(Handle_NetworkBattleApproved);
    }

    // --- Creation: server rolls once, broadcasts the resolved quest village ---

    private void Handle_MerchantArmyOfPoachersIssueCreated(MessagePayload<MerchantArmyOfPoachersIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureQuestVillage(issue, out var questVillage))
        {
            Logger.Error("Could not capture Merchant Army of Poachers issue village for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(questVillage, out var questVillageId)) return;

        network.SendAll(new NetworkMerchantArmyOfPoachersIssueCreated(ownerId, questVillageId));
    }

    private void Handle_NetworkMerchantArmyOfPoachersIssueCreated(MessagePayload<NetworkMerchantArmyOfPoachersIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Village>(data.QuestVillageId, out var questVillage)) return;

            var replicated = issueInterface.ConstructReplicated(owner, questVillage);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Poachers-party spawn gate: see Patches.MerchantArmyOfPoachersPartySpawnGatePatch's doc comment ---

    private void Handle_PartySpawnRequested(MessagePayload<MerchantArmyOfPoachersPartySpawnRequested> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        // Only ever published on a client (see the Patch's Postfix) - a client's own SendAll only ever reaches
        // the server.
        network.SendAll(new RequestMerchantArmyOfPoachersPartySpawn(ownerId));
    }

    private void Handle_RequestPartySpawn(MessagePayload<RequestMerchantArmyOfPoachersPartySpawn> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (owner.Issue?.IssueQuest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest)
            {
                Logger.Error("No MerchantArmyOfPoachersIssueQuest mirror available yet for owner {Owner}", data.OwnerId);
                return;
            }

            // Idempotent (CreatePoachersPartyOnServer itself already no-ops if the party exists) - a resend
            // must not spawn (or re-broadcast) a second poachers party for the same quest.
            var party = issueInterface.CreatePoachersPartyOnServer(owner);
            if (party == null)
            {
                Logger.Error("Failed to create the replicated Merchant Army of Poachers party for owner {Owner}", data.OwnerId);
                return;
            }

            messageBroker.Publish(this, new MerchantArmyOfPoachersPartySpawned(owner, party));
        });
    }

    private void Handle_PartySpawned(MessagePayload<MerchantArmyOfPoachersPartySpawned> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        if (data.Owner == null || data.PoachersParty == null) return;
        if (!objectManager.TryGetIdWithLogging(data.Owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(data.PoachersParty, out var poachersPartyId)) return;

        network.SendAll(new NetworkMerchantArmyOfPoachersPartySpawned(ownerId, poachersPartyId));
    }

    private void Handle_NetworkPartySpawned(MessagePayload<NetworkMerchantArmyOfPoachersPartySpawned> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.PoachersPartyId, out var poachersParty)) return;

            issueInterface.ForcePoachersParty(owner, poachersParty);
        });
    }

    // --- Battle-start approval: see Patches.MerchantArmyOfPoachersBattleStartApprovalPatches's doc comment ---

    private void Handle_BattleStartRequested(MessagePayload<MerchantArmyOfPoachersBattleStartRequested> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new NetworkMerchantArmyOfPoachersBattleStartRequest(ownerId));
    }

    private void Handle_NetworkBattleStartRequest(MessagePayload<NetworkMerchantArmyOfPoachersBattleStartRequest> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (owner.Issue?.IssueQuest is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest)
            {
                Logger.Error("No MerchantArmyOfPoachersIssueQuest mirror available yet for owner {Owner}", data.OwnerId);
                return;
            }

            // Sanity check (same trust model as every other request-forward handler in this family - the
            // server trusts that only the client-side ownership-gated Prefix ever sends this): the poachers
            // party must exist before a fight against it can be approved.
            if (!issueInterface.TryCapturePoachersParty(owner, out _))
            {
                Logger.Error("Battle start requested for owner {Owner} before its poachers party exists", data.OwnerId);
                return;
            }

            messageBroker.Publish(this, new MerchantArmyOfPoachersBattleApproved(owner));
        });
    }

    private void Handle_BattleApproved(MessagePayload<MerchantArmyOfPoachersBattleApproved> payload)
    {
        if (ModInformation.IsClient) return;

        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new NetworkMerchantArmyOfPoachersBattleApproved(ownerId));
    }

    private void Handle_NetworkBattleApproved(MessagePayload<NetworkMerchantArmyOfPoachersBattleApproved> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner))
            {
                // The genuine owner's own machine (host or client, whichever this is) - actually launch the
                // mission, the one thing that can never be relocated to the server (see
                // IMerchantArmyOfPoachersIssueInterface's type doc comment).
                issueInterface.InvokeRealStartQuestBattle(owner);
            }
            else
            {
                // Parity-only bookkeeping for every other peer's mirror - see ForceIsReadyToBeFinalized's doc
                // comment.
                issueInterface.ForceIsReadyToBeFinalized(owner);
            }
        });
    }
}
