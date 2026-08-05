using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Village Needs Tools issue replication.
///
/// Creation: the server captures the terms an already-vetted-server-side <c>IssueManager.CreateNewIssue</c>
/// call rolled and broadcasts them, so every client builds a byte-identical instance instead of
/// independently re-deriving one (see IssueManagerCreateNewIssuePatches / VillageNeedsToolsIssueInterface).
///
/// Acceptance: whichever machine's own live conversation accepts (quest solution or alternative solution)
/// already applied that transition locally for real (see IssueAcceptancePatches for why it can't be
/// blocked pending confirmation). That genuine trigger is routed through the server, which arbitrates a
/// same-issue double-accept race (first valid request wins) and either confirms it to every other peer
/// (who then mirror the same transition) or rejects it back to the one losing requester (who rolls their
/// own already-applied local copy back) - see Finding 1 in the review this fixes.
///
/// Removal: whichever machine reaches a genuine (non-mirrored) <c>IssueFinalized</c> - the server for an
/// ambient-tick timeout, or the one client in the quest-turn-in conversation for a success - routes it
/// through the server so every other peer mirrors the same teardown, using the correct
/// <c>QuestBase.CompleteQuestWithXxx</c>/<c>IssueBase.CompleteIssueWithXxx</c> for the captured reason
/// instead of a bare <c>IssueFinalized()</c> that would orphan an active mirrored quest (see
/// IssueFinalizedPatches).
///
/// Real ownership (fixes both a "anyone can turn in someone else's accepted quest" bug and a "the
/// alternative-solution reward/timer fires on the wrong machine, or crashes a dedicated host, forever" bug):
/// every accept confirmation now also carries the accepting player's ControllerId, recorded into
/// <see cref="VillageNeedsToolsIssueOwnership"/> by every peer (including the accepter) from the SAME
/// broadcast that drives the existing mirror call, in the same synchronous block - so there is never a
/// window where a peer has mirrored the accept but not yet recorded who owns it. For the arbitrated-race
/// path (<see cref="Handle_RequestVillageIssueAcceptQuest"/>/<see cref="Handle_RequestVillageIssueAcceptAlternative"/>),
/// that ControllerId is resolved server-side from the authenticated requesting <c>NetPeer</c> via
/// <c>IPlayerManager.TryGetPlayer(NetPeer, out Player)</c> - NEVER trusted from a client-supplied
/// value, which is the load-bearing security point: a client cannot claim a different ControllerId to steal
/// ownership of someone else's accept.
/// </summary>
internal class VillageNeedsToolsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsToolsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IVillageNeedsToolsIssueInterface issueInterface;
    private readonly IPlayerManager playerManager;

    public VillageNeedsToolsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IVillageNeedsToolsIssueInterface issueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Subscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);

        messageBroker.Subscribe<VillageIssueQuestAcceptTriggered>(Handle_VillageIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestVillageIssueAcceptQuest>(Handle_RequestVillageIssueAcceptQuest);
        messageBroker.Subscribe<NetworkVillageIssueQuestAccepted>(Handle_NetworkVillageIssueQuestAccepted);

        messageBroker.Subscribe<VillageIssueAlternativeAcceptTriggered>(Handle_VillageIssueAlternativeAcceptTriggered);
        messageBroker.Subscribe<RequestVillageIssueAcceptAlternative>(Handle_RequestVillageIssueAcceptAlternative);
        messageBroker.Subscribe<NetworkVillageIssueAlternativeAccepted>(Handle_NetworkVillageIssueAlternativeAccepted);

        messageBroker.Subscribe<NetworkVillageIssueAcceptRejected>(Handle_NetworkVillageIssueAcceptRejected);

        messageBroker.Subscribe<VillageIssueFinalizedTriggered>(Handle_VillageIssueFinalizedTriggered);
        messageBroker.Subscribe<RequestVillageIssueRemoved>(Handle_RequestVillageIssueRemoved);
        messageBroker.Subscribe<NetworkVillageIssueRemoved>(Handle_NetworkVillageIssueRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);

        messageBroker.Unsubscribe<VillageIssueQuestAcceptTriggered>(Handle_VillageIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageIssueAcceptQuest>(Handle_RequestVillageIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkVillageIssueQuestAccepted>(Handle_NetworkVillageIssueQuestAccepted);

        messageBroker.Unsubscribe<VillageIssueAlternativeAcceptTriggered>(Handle_VillageIssueAlternativeAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageIssueAcceptAlternative>(Handle_RequestVillageIssueAcceptAlternative);
        messageBroker.Unsubscribe<NetworkVillageIssueAlternativeAccepted>(Handle_NetworkVillageIssueAlternativeAccepted);

        messageBroker.Unsubscribe<NetworkVillageIssueAcceptRejected>(Handle_NetworkVillageIssueAcceptRejected);

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

    // --- Acceptance (Finding 1): the accepting machine already applied it locally for real; the server
    // arbitrates a same-issue double-accept race and confirms/rejects it. ---

    private void Handle_VillageIssueQuestAcceptTriggered(MessagePayload<VillageIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            // The host's own live conversation just accepted - already authoritative. The host's own
            // ControllerId (captured at accept time by IssueAcceptancePatches) IS the real owner here -
            // record it locally too, exactly like every other peer will when this broadcast comes back to
            // them (see Handle_NetworkVillageIssueQuestAccepted) - this is the "genuine accepter" case.
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageIssueQuestAccepted(ownerId, hostControllerId));
        }
        else
        {
            // A client's own live conversation already applied this locally (see IssueAcceptancePatches) -
            // tell the server so it can arbitrate a same-issue double-accept race. Deliberately no
            // ControllerId field here: identity is derived server-side from the authenticated NetPeer (see
            // Handle_RequestVillageIssueAcceptQuest), never trusted from a client-claimed value.
            network.SendAll(new RequestVillageIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestVillageIssueAcceptQuest(MessagePayload<RequestVillageIssueAcceptQuest> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            // The accepting player's identity is derived server-side from the authenticated NetPeer via
            // IPlayerManager - never trusted from a client-claimed value. This is the load-bearing security
            // point: a client cannot claim a different ControllerId to steal ownership of someone else's
            // accept.
            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestVillageIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
                return;
            }

            if (GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(owner.Issue) && owner.Issue.IsOngoingWithoutQuest)
            {
                // First valid request wins the race - replay on the server's own authoritative copy, then
                // confirm to everyone (including the requester, harmlessly idempotent for them since they
                // already applied it locally).
                issueInterface.MirrorQuestAccepted(owner);
                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageIssueQuestAccepted(ownerId, player.ControllerId));
            }
            else
            {
                // Someone else already won the race (or the issue is gone) - tell just this requester to
                // roll their own already-applied local copy back.
                network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkVillageIssueQuestAccepted(MessagePayload<NetworkVillageIssueQuestAccepted> payload)
    {
        var ownerId = payload.What.OwnerId;
        var ownerControllerId = payload.What.OwnerControllerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            // Mirror and ownership record are set in the same synchronous block driven off the same
            // broadcast, so a non-owner peer can never observe "dialogue registered" without "ownership
            // known" already being true in the same instant - no TOCTOU window.
            issueInterface.MirrorQuestAccepted(owner);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, ownerControllerId);
        });
    }

    private void Handle_VillageIssueAlternativeAcceptTriggered(MessagePayload<VillageIssueAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageIssueAlternativeAccepted(ownerId, hostControllerId));
        }
        else
        {
            network.SendAll(new RequestVillageIssueAcceptAlternative(ownerId));
        }
    }

    private void Handle_RequestVillageIssueAcceptAlternative(MessagePayload<RequestVillageIssueAcceptAlternative> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestVillageIssueAcceptAlternative), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
                return;
            }

            if (GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(owner.Issue) && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.MirrorAlternativeAccepted(owner);
                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageIssueAlternativeAccepted(ownerId, player.ControllerId));
            }
            else
            {
                network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkVillageIssueAlternativeAccepted(MessagePayload<NetworkVillageIssueAlternativeAccepted> payload)
    {
        var ownerId = payload.What.OwnerId;
        var ownerControllerId = payload.What.OwnerControllerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.MirrorAlternativeAccepted(owner);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, ownerControllerId);
        });
    }

    private void Handle_NetworkVillageIssueAcceptRejected(MessagePayload<NetworkVillageIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.RejectAcceptance(owner);
        });
    }

    // --- Removal: whichever machine genuinely finalizes routes through the server so every peer mirrors it ---

    private void Handle_VillageIssueFinalizedTriggered(MessagePayload<VillageIssueFinalizedTriggered> payload)
    {
        var owner = payload.What.Owner;
        var reason = payload.What.Reason;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            network.SendAll(new NetworkVillageIssueRemoved(ownerId, reason));
        }
        else
        {
            // A client's SendAll only reaches its one connection - the server - which replays the finalize
            // on its own copy (Handle_RequestVillageIssueRemoved) and broadcasts the real removal from there.
            network.SendAll(new RequestVillageIssueRemoved(ownerId, reason));
        }
    }

    private void Handle_RequestVillageIssueRemoved(MessagePayload<RequestVillageIssueRemoved> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var reason = payload.What.Reason;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            // Mirrors the requesting client's finalize on the server's own copy - a no-op if it was already
            // finalized server-side first.
            issueInterface.FinalizeMirror(owner, reason);

            network.SendAll(new NetworkVillageIssueRemoved(ownerId, reason));
        });
    }

    private void Handle_NetworkVillageIssueRemoved(MessagePayload<NetworkVillageIssueRemoved> payload)
    {
        var ownerId = payload.What.OwnerId;
        var reason = payload.What.Reason;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.FinalizeMirror(owner, reason);
        });
    }
}
