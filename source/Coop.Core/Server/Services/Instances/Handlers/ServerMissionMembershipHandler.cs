using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Network.Session;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using System;
using System.Net;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// Tracks relay membership, introduces both sides with <see cref="NetworkMissionPeerEntered"/>, and fans
/// graceful or disconnected departures to the remaining mission members.
/// </summary>
public class ServerMissionMembershipHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerMissionMembershipHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IMissionManager missionManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IAuthenticatedPeerIdentityResolver peerIdentityResolver;

    public ServerMissionMembershipHandler(
        IMessageBroker messageBroker,
        IMissionManager missionManager,
        INetwork network,
        IPlayerManager playerManager)
        : this(messageBroker, missionManager, network, playerManager, null)
    {
    }

    public ServerMissionMembershipHandler(
        IMessageBroker messageBroker,
        IMissionManager missionManager,
        INetwork network,
        IPlayerManager playerManager,
        IAuthenticatedPeerIdentityResolver peerIdentityResolver)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;
        this.network = network;
        this.playerManager = playerManager;
        this.peerIdentityResolver = peerIdentityResolver;

        messageBroker.Subscribe<NetworkMissionEntered>(Handle_MissionEntered);
        messageBroker.Subscribe<NetworkMissionLeft>(Handle_MissionLeft);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMissionEntered>(Handle_MissionEntered);
        messageBroker.Unsubscribe<NetworkMissionLeft>(Handle_MissionLeft);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    private void Handle_MissionEntered(MessagePayload<NetworkMissionEntered> payload)
    {
        if (payload.Who is not NetPeer peer)
            return;

        var message = payload.What;
        if (!TryGetCurrentController(peer, out var controllerId))
        {
            Logger.Debug("Ignoring mission entry for instance {Instance} from stale or unregistered peer {Peer}",
                message.InstanceId, peer);
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!missionManager.TryEnterMission(peer, controllerId, message.InstanceId, out var result) ||
                result.Status == MissionEntryStatus.Unchanged)
            {
                return;
            }

            foreach (var departure in result.PreviousDepartures)
                PublishDeparture(departure, wasRetreat: true);

            // A replacement peer must report battle completion again even though membership is preserved.
            messageBroker.Publish(this,
                new MissionMemberEntered(result.ControllerId, result.InstanceId, result.IsFirstMember));

            // Introduce the newcomer and each existing member to each other so BOTH sides send their join info.
            var newcomerIdentity = ResolveIdentity(peer, result.ControllerId);
            foreach (var (otherControllerId, otherPeer) in result.ExistingMembers)
            {
                var existingIdentity = ResolveIdentity(otherPeer, otherControllerId);

                network.Send(otherPeer, new NetworkMissionPeerEntered(
                    result.ControllerId, result.InstanceId, newcomerIdentity));
                network.Send(peer, new NetworkMissionPeerEntered(
                    otherControllerId, result.InstanceId, existingIdentity));
            }
        }, context: nameof(Handle_MissionEntered));
    }

    private PlatformIdentity ResolveIdentity(NetPeer peer, string expectedControllerId)
    {
        var endpoint = new IPEndPoint(peer.Address, peer.Port);
        return peerIdentityResolver != null &&
            peerIdentityResolver.TryGetIdentity(endpoint, out var identity) &&
            string.Equals(identity.ControllerId, expectedControllerId, StringComparison.Ordinal)
            ? identity
            : default;
    }

    private void Handle_MissionLeft(MessagePayload<NetworkMissionLeft> payload)
    {
        if (payload.Who is not NetPeer peer)
            return;

        var message = payload.What;
        if (!TryGetCurrentController(peer, out var controllerId))
        {
            Logger.Debug("Ignoring mission leave for instance {Instance} from stale or unregistered peer {Peer}",
                message.InstanceId, peer);
            return;
        }

        missionManager.RevokeRelay(peer);

        GameThread.RunSafe(() =>
        {
            if (!missionManager.TryLeaveMission(peer, controllerId, message.InstanceId, out var departure))
                return;

            PublishDeparture(departure, wasRetreat: true);
        }, context: nameof(Handle_MissionLeft));
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        var peer = payload.What.PlayerId;
        missionManager.RevokeRelay(peer);

        GameThread.RunSafe(() =>
        {
            var departures = missionManager.HandleDisconnect(peer);
            foreach (var departure in departures)
                PublishDeparture(departure, wasRetreat: false);
        }, context: nameof(Handle_PlayerDisconnected));
    }

    private bool TryGetCurrentController(NetPeer peer, out string controllerId)
    {
        controllerId = null;
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !playerManager.TryGetPeer(player.ControllerId, out var currentPeer) ||
            !object.ReferenceEquals(currentPeer, peer))
        {
            return false;
        }

        controllerId = player.ControllerId;
        return true;
    }

    private void PublishDeparture(MissionDeparture departure, bool wasRetreat)
    {
        foreach (var (_, otherPeer) in departure.RemainingMembers)
        {
            if (wasRetreat)
                network.Send(otherPeer, new MissionPeerLeft(departure.ControllerId, departure.InstanceId));
            else
                network.Send(otherPeer, new MissionPeerDisconnected(departure.ControllerId, departure.InstanceId));
        }

        messageBroker.Publish(this, new MissionMemberDeparted(
            departure.ControllerId,
            departure.InstanceId,
            wasRetreat,
            departure.IsInstanceEmpty));
    }
}
