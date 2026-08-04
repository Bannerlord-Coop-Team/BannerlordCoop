using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Blocking, server-authoritative creation of Rival Gang Moving In's 2 scripted parties + 2 henchman Heroes -
/// modeled directly on <see cref="GameInterface.Services.MapEvents.Handlers.MapEventCreationCoordinator.RequestBlocking"/>
/// (same <c>GameThread.WaitWhilePumping</c>/<see cref="Common.Network.INetworkConfig.ObjectCreationTimeout"/>/
/// <c>PendingRequest</c>/<see cref="ManualResetEventSlim"/> shape).
///
/// Unlike every other party-spawn gate in this issue family (Smugglers/Caravan Ambush/Merchant Army of
/// Poachers, all fire-and-forget - the party arrives later via a broadcast the caller tolerates), this MUST
/// block: <c>StartAlleyBattle()</c>'s very next lines after
/// <c>CreateRivalGangLeaderParty()</c>/<c>CreateAllyGangLeaderParty()</c> immediately dereference
/// <c>_rivalGangLeaderParty.Party</c>/<c>_allyGangLeaderParty</c> - see
/// <see cref="Patches.RivalGangMovingInPartySpawnGatePatch"/>'s doc comment. One request
/// (<see cref="NetworkRequestCreateRivalGangParties"/>) creates BOTH parties + BOTH henchman Heroes server-side
/// in a single game-thread-blocking call (<see cref="IRivalGangMovingInIssueInterface.CreateReplicatedParties"/>),
/// then replies with all 4 object-manager ids once the AutoRegistry sync for the 2 <see cref="MobileParty"/>s +
/// 2 <see cref="Hero"/>s has landed on the requesting client; the client force-writes all 4 quest fields before
/// <see cref="RequestBlocking"/> returns.
///
/// Rejection/timeout: returns false and leaves the quest's 4 fields null.
/// <see cref="Patches.RivalGangMovingInAlleyBattlePatch"/> checks for exactly this and aborts gracefully instead
/// of letting <c>StartAlleyBattle()</c> continue into a null-party NRE - see that type's doc comment.
/// </summary>
internal class RivalGangMovingInPartyCreationCoordinator : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RivalGangMovingInPartyCreationCoordinator>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfig configuration;
    private readonly IRivalGangMovingInIssueInterface issueInterface;
    private readonly ConcurrentDictionary<string, PendingRequest> pendingRequests = new ConcurrentDictionary<string, PendingRequest>();

    public RivalGangMovingInPartyCreationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        INetworkConfig configuration,
        IRivalGangMovingInIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.configuration = configuration;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<NetworkRequestCreateRivalGangParties>(Handle_NetworkRequestCreateRivalGangParties);
        messageBroker.Subscribe<NetworkRivalGangPartiesCreated>(Handle_NetworkRivalGangPartiesCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestCreateRivalGangParties>(Handle_NetworkRequestCreateRivalGangParties);
        messageBroker.Unsubscribe<NetworkRivalGangPartiesCreated>(Handle_NetworkRivalGangPartiesCreated);
    }

    /// <summary>[Client] Blocks until the server creates both scripted parties + both henchman Heroes and their
    /// AutoRegistry sync is committed on this client, force-writing all 4 quest fields before returning. Returns
    /// false (and leaves the fields untouched) on rejection or timeout.</summary>
    public bool RequestBlocking(Hero owner)
    {
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId))
            return false;

        var requestId = Guid.NewGuid().ToString();
        var pending = new PendingRequest();
        pendingRequests[requestId] = pending;

        try
        {
            var timeout = configuration.ObjectCreationTimeout;
            var deadline = DateTime.UtcNow + timeout;

            Logger.Debug(
                "Requesting authoritative rival gang party creation from server. RequestId={RequestId}, OwnerId={OwnerId}",
                requestId, ownerId);

            // On a client, SendAll targets the server (its only connected peer).
            network.SendAll(new NetworkRequestCreateRivalGangParties(requestId, ownerId));

            if (!GameThread.WaitWhilePumping(() => pending.Completed.IsSet, deadline))
            {
                Logger.Error(
                    "Timed out after {Timeout} waiting for the server to create the rival gang parties. RequestId={RequestId}",
                    timeout, requestId);
                return false;
            }

            if (pending.Outcome != RivalGangPartyCreationOutcome.Created)
            {
                Logger.Error(
                    "Server could not create the rival gang parties. Outcome={Outcome}, RequestId={RequestId}",
                    pending.Outcome, requestId);
                return false;
            }

            MobileParty rivalGangLeaderParty = null;
            Hero rivalGangLeaderHenchmanHero = null;
            MobileParty allyGangLeaderParty = null;
            Hero allyGangLeaderHenchmanHero = null;

            // The reply can wake this request before the queued AutoRegistry creation commits has run.
            if (!GameThread.WaitWhilePumping(
                    () => objectManager.TryGetObject(pending.RivalGangLeaderPartyId, out rivalGangLeaderParty) && rivalGangLeaderParty != null
                        && objectManager.TryGetObject(pending.RivalGangLeaderHenchmanHeroId, out rivalGangLeaderHenchmanHero) && rivalGangLeaderHenchmanHero != null
                        && objectManager.TryGetObject(pending.AllyGangLeaderPartyId, out allyGangLeaderParty) && allyGangLeaderParty != null
                        && objectManager.TryGetObject(pending.AllyGangLeaderHenchmanHeroId, out allyGangLeaderHenchmanHero) && allyGangLeaderHenchmanHero != null,
                    deadline))
            {
                Logger.Error(
                    "Server created the rival gang parties but they were not committed on this client before timeout. RequestId={RequestId}",
                    requestId);
                return false;
            }

            issueInterface.ForceRivalGangParties(owner, rivalGangLeaderParty, rivalGangLeaderHenchmanHero, allyGangLeaderParty, allyGangLeaderHenchmanHero);

            Logger.Debug("Resolved server-created rival gang parties. RequestId={RequestId}", requestId);
            return true;
        }
        finally
        {
            pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>[Server] Create both parties + henchman Heroes authoritatively and reply with their ids.</summary>
    private void Handle_NetworkRequestCreateRivalGangParties(MessagePayload<NetworkRequestCreateRivalGangParties> payload)
    {
        if (ModInformation.IsClient) return;

        var request = payload.What;
        var requestingPeer = payload.Who as NetPeer;

        GameThread.RunSafe(
            () => CreateAndReply(requestingPeer, request),
            blocking: true,
            context: nameof(Handle_NetworkRequestCreateRivalGangParties));
    }

    private void CreateAndReply(NetPeer requestingPeer, NetworkRequestCreateRivalGangParties request)
    {
        if (requestingPeer == null)
        {
            Logger.Error("Received {Message} with no originating peer. RequestId={RequestId}",
                nameof(NetworkRequestCreateRivalGangParties), request.RequestId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Hero>(request.OwnerId, out var owner))
        {
            SendReply(requestingPeer, request.RequestId, RivalGangPartyCreationOutcome.Rejected, null, null, null, null);
            return;
        }

        if (!issueInterface.CreateReplicatedParties(owner, out var rivalGangLeaderParty, out var rivalGangLeaderHenchmanHero,
                out var allyGangLeaderParty, out var allyGangLeaderHenchmanHero))
        {
            Logger.Error("Failed to create the rival gang parties on the server for owner {OwnerId}. RequestId={RequestId}",
                request.OwnerId, request.RequestId);
            SendReply(requestingPeer, request.RequestId, RivalGangPartyCreationOutcome.Rejected, null, null, null, null);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(rivalGangLeaderParty, out var rivalGangLeaderPartyId) ||
            !objectManager.TryGetIdWithLogging(rivalGangLeaderHenchmanHero, out var rivalGangLeaderHenchmanHeroId) ||
            !objectManager.TryGetIdWithLogging(allyGangLeaderParty, out var allyGangLeaderPartyId) ||
            !objectManager.TryGetIdWithLogging(allyGangLeaderHenchmanHero, out var allyGangLeaderHenchmanHeroId))
        {
            Logger.Error(
                "Created the rival gang parties on the server but could not resolve their ids for owner {OwnerId}. RequestId={RequestId}",
                request.OwnerId, request.RequestId);
            SendReply(requestingPeer, request.RequestId, RivalGangPartyCreationOutcome.Unresolved, null, null, null, null);
            return;
        }

        SendReply(requestingPeer, request.RequestId, RivalGangPartyCreationOutcome.Created,
            rivalGangLeaderPartyId, rivalGangLeaderHenchmanHeroId, allyGangLeaderPartyId, allyGangLeaderHenchmanHeroId);
    }

    private void SendReply(
        NetPeer requestingPeer,
        string requestId,
        RivalGangPartyCreationOutcome outcome,
        string rivalGangLeaderPartyId,
        string rivalGangLeaderHenchmanHeroId,
        string allyGangLeaderPartyId,
        string allyGangLeaderHenchmanHeroId)
    {
        Logger.Debug(
            "Server resolved rival gang party creation request with {Outcome}. RequestId={RequestId}",
            outcome, requestId);
        network.Send(requestingPeer, new NetworkRivalGangPartiesCreated(
            requestId, outcome, rivalGangLeaderPartyId, rivalGangLeaderHenchmanHeroId, allyGangLeaderPartyId, allyGangLeaderHenchmanHeroId));
    }

    /// <summary>[Client] Complete the pending blocking request with the server-assigned ids.</summary>
    private void Handle_NetworkRivalGangPartiesCreated(MessagePayload<NetworkRivalGangPartiesCreated> payload)
    {
        var message = payload.What;

        if (!pendingRequests.TryGetValue(message.RequestId, out var pending))
        {
            // Late arrival (already timed out and removed) or a response for another instance.
            Logger.Warning("Received {Message} for unknown or expired RequestId={RequestId}",
                nameof(NetworkRivalGangPartiesCreated), message.RequestId);
            return;
        }

        pending.Outcome = message.Outcome;
        pending.RivalGangLeaderPartyId = message.RivalGangLeaderPartyId;
        pending.RivalGangLeaderHenchmanHeroId = message.RivalGangLeaderHenchmanHeroId;
        pending.AllyGangLeaderPartyId = message.AllyGangLeaderPartyId;
        pending.AllyGangLeaderHenchmanHeroId = message.AllyGangLeaderHenchmanHeroId;
        pending.Completed.Set();
    }

    /// <summary>Tracks a single in-flight request. <see cref="Completed"/> is deliberately not disposed - same
    /// rationale as <see cref="GameInterface.Services.MapEvents.Handlers.MapEventCreationCoordinator.PendingRequest"/>.</summary>
    private sealed class PendingRequest
    {
        public ManualResetEventSlim Completed { get; } = new ManualResetEventSlim(false);
        public RivalGangPartyCreationOutcome Outcome { get; set; } = RivalGangPartyCreationOutcome.Unresolved;
        public string RivalGangLeaderPartyId { get; set; }
        public string RivalGangLeaderHenchmanHeroId { get; set; }
        public string AllyGangLeaderPartyId { get; set; }
        public string AllyGangLeaderHenchmanHeroId { get; set; }
    }
}
