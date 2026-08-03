using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Village Needs Tools issue replication. Creation: the server captures the terms an
/// already-vetted-server-side <c>IssueManager.CreateNewIssue</c> call rolled and broadcasts them, so every
/// client builds a byte-identical instance instead of independently re-deriving one (see
/// IssueManagerCreateNewIssuePatches / VillageNeedsToolsIssueInterface). Removal: whichever machine reaches
/// a genuine (non-mirrored) <c>IssueFinalized</c> - the server for an ambient-tick timeout, or the one
/// client in the quest-turn-in conversation for a success - routes it through the server so every other
/// peer mirrors the same teardown (see IssueFinalizedPatches).
/// </summary>
internal class VillageNeedsToolsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsToolsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IVillageNeedsToolsIssueInterface issueInterface;

    public VillageNeedsToolsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IVillageNeedsToolsIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Subscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);

        messageBroker.Subscribe<VillageIssueFinalizedTriggered>(Handle_VillageIssueFinalizedTriggered);
        messageBroker.Subscribe<RequestVillageIssueRemoved>(Handle_RequestVillageIssueRemoved);
        messageBroker.Subscribe<NetworkVillageIssueRemoved>(Handle_NetworkVillageIssueRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);

        messageBroker.Unsubscribe<VillageIssueFinalizedTriggered>(Handle_VillageIssueFinalizedTriggered);
        messageBroker.Unsubscribe<RequestVillageIssueRemoved>(Handle_RequestVillageIssueRemoved);
        messageBroker.Unsubscribe<NetworkVillageIssueRemoved>(Handle_NetworkVillageIssueRemoved);
    }

    // --- Creation: server rolls once, broadcasts the resolved terms ---

    private void Handle_VillageIssueCreated(MessagePayload<VillageIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var requestedItem, out var exchangeItem,
            out var numberOfExchangeItem, out var numberOfRequestedItem, out var payment))
        {
            Logger.Error("Could not capture Village Needs Tools issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(requestedItem, out var requestedItemId)) return;

        string exchangeItemId = null;
        if (exchangeItem != null && !objectManager.TryGetIdWithLogging(exchangeItem, out exchangeItemId)) return;

        network.SendAll(new NetworkVillageIssueCreated(
            ownerId, requestedItemId, exchangeItemId, numberOfRequestedItem, numberOfExchangeItem, payment));
    }

    private void Handle_NetworkVillageIssueCreated(MessagePayload<NetworkVillageIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame, since IssueManager's own dictionary is a real SaveableField) must not create
            // a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedItemId, out var requestedItem)) return;

            ItemObject exchangeItem = null;
            if (data.ExchangeItemId != null &&
                !objectManager.TryGetObjectWithLogging<ItemObject>(data.ExchangeItemId, out exchangeItem)) return;

            var replicated = issueInterface.ConstructReplicated(
                owner, requestedItem, exchangeItem, data.NumberOfExchangeItem, data.NumberOfRequestedItem, data.Payment);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Removal: whichever machine genuinely finalizes routes through the server so every peer mirrors it ---

    private void Handle_VillageIssueFinalizedTriggered(MessagePayload<VillageIssueFinalizedTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            network.SendAll(new NetworkVillageIssueRemoved(ownerId));
        }
        else
        {
            // A client's SendAll only reaches its one connection - the server - which replays the finalize
            // on its own copy (Handle_RequestVillageIssueRemoved) and broadcasts the real removal from there.
            network.SendAll(new RequestVillageIssueRemoved(ownerId));
        }
    }

    private void Handle_RequestVillageIssueRemoved(MessagePayload<RequestVillageIssueRemoved> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            // Mirrors the requesting client's finalize on the server's own copy. IssueFinalized only tears
            // down bookkeeping (dictionary removal, hero.Issue backref) - it never re-applies rewards - so
            // replaying it here is safe, and a no-op if it was already finalized server-side first.
            issueInterface.FinalizeMirror(owner);

            network.SendAll(new NetworkVillageIssueRemoved(ownerId));
        });
    }

    private void Handle_NetworkVillageIssueRemoved(MessagePayload<NetworkVillageIssueRemoved> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.FinalizeMirror(owner);
        });
    }
}
