using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Network.Session;
using Coop.Core.Common.Session;
using LiteNetLib;
using Missions.Messages;
using System.Globalization;
using System.Net;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// Tracks relay membership, introduces both sides with <see cref="NetworkMissionPeerEntered"/>, and fans
/// graceful or disconnected departures to the remaining mission members.
/// </summary>
public class ServerMissionMembershipHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMissionManager missionManager;
    private readonly INetwork network;
    private readonly ISessionTunnelIdentityResolver tunnelIdentityResolver;

    public ServerMissionMembershipHandler(
        IMessageBroker messageBroker,
        IMissionManager missionManager,
        INetwork network)
        : this(messageBroker, missionManager, network, null)
    {
    }

    public ServerMissionMembershipHandler(
        IMessageBroker messageBroker,
        IMissionManager missionManager,
        INetwork network,
        ISessionTunnelIdentityResolver tunnelIdentityResolver)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;
        this.network = network;
        this.tunnelIdentityResolver = tunnelIdentityResolver;

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
        var peer = (NetPeer)payload.Who;
        var message = payload.What;

        if (!missionManager.TryEnterMission(
                peer,
                message.ControllerId,
                message.InstanceId,
                out var others,
                out var isFirstMember))
        {
            return;
        }

        messageBroker.Publish(this,
            new MissionMemberEntered(message.ControllerId, message.InstanceId, isFirstMember));

        // Introduce the newcomer and each existing member to each other so BOTH sides send their join info
        // (replaces the direct PeerConnected trigger). The introduction travels over the campaign/relay
        // connection; the join info itself still flows over the IBattleNetwork mesh.
        var newcomerSteamId = ResolveSteamId(peer, message.ControllerId);
        foreach (var (otherControllerId, otherPeer) in others)
        {
            var existingSteamId = ResolveSteamId(otherPeer, otherControllerId);

            network.Send(otherPeer, new NetworkMissionPeerEntered(
                message.ControllerId, message.InstanceId, newcomerSteamId));
            network.Send(peer, new NetworkMissionPeerEntered(
                otherControllerId, message.InstanceId, existingSteamId));
        }
    }

    private ulong ResolveSteamId(NetPeer peer, string controllerId)
    {
        var endpoint = new IPEndPoint(peer.Address, peer.Port);
        if (tunnelIdentityResolver != null
            && tunnelIdentityResolver.TryGetRemoteSteamId(endpoint, out var steamId))
            return steamId;

        // The hosting client reaches its spawned server directly over loopback, so it has no tunnel
        // endpoint to map. In Release its controller id is its Steam id; constrain that fallback to a
        // managed server's local peer so arbitrary direct-IP controller ids are never treated as Steam.
        if (ManagedServerConfig.IsManagedServer
            && IPAddress.IsLoopback(peer.Address)
            && ulong.TryParse(controllerId, NumberStyles.None, CultureInfo.InvariantCulture, out steamId))
        {
            return steamId;
        }

        return 0;
    }

    private void Handle_MissionLeft(MessagePayload<NetworkMissionLeft> payload)
    {
        var peer = (NetPeer)payload.Who;
        var message = payload.What;

        var remaining = missionManager.LeaveMission(peer, message.ControllerId, message.InstanceId);

        // Mirror the entry fan-out: tell the members still present that the controller is gone so they
        // despawn its party.
        foreach (var (_, otherPeer) in remaining)
        {
            network.Send(otherPeer, new MissionPeerLeft(message.ControllerId, message.InstanceId));
        }

        // Local signal for battle host migration / successor cleanup (no-op for non-battle instances). A
        // graceful leave is a retreat — the battle reserve forgets the party so a rejoin re-spawns.
        messageBroker.Publish(this, new MissionMemberDeparted(
            message.ControllerId,
            message.InstanceId,
            wasRetreat: true,
            isInstanceEmpty: remaining.Count == 0));
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        // PlayerDisconnected fires for every disconnect; only act on a peer that was in a mission instance.
        if (missionManager.TryHandleDisconnect(payload.What.PlayerId, out var controllerId, out var instanceId, out var remaining) == false)
            return;

        foreach (var (_, otherPeer) in remaining)
        {
            network.Send(otherPeer, new MissionPeerDisconnected(controllerId, instanceId));
        }

        // Local signal for battle host migration / successor cleanup (no-op for non-battle instances). A drop
        // withdraws the player's party like a retreat, so forget its reserve and re-spawn it fresh on rejoin.
        messageBroker.Publish(this, new MissionMemberDeparted(
            controllerId,
            instanceId,
            wasRetreat: true,
            isInstanceEmpty: remaining.Count == 0));
    }
}
