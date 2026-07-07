using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using LiteNetLib;
using Missions.Messages;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// Server-side relay membership + join/leave introduction. A client's <see cref="NetworkMissionEntered"/> maps the
/// controller to its connection and introduces it and the existing members to each other via
/// <see cref="NetworkMissionPeerEntered"/> (each side then sends its join info over the mesh). Departures are fanned
/// out to the remaining members so they release the leaver's party: a <see cref="NetworkMissionLeft"/> becomes a
/// <see cref="MissionPeerLeft"/> (graceful) and an observed <see cref="PlayerDisconnected"/> becomes a
/// <see cref="MissionPeerDisconnected"/> (ungraceful — the reliable counterpart to the best-effort mesh path).
/// </summary>
public class ServerMissionMembershipHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMissionManager missionManager;
    private readonly INetwork network;

    public ServerMissionMembershipHandler(IMessageBroker messageBroker, IMissionManager missionManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;
        this.network = network;

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

        var others = missionManager.EnterMission(peer, message.ControllerId, message.InstanceId);

        // Introduce the newcomer and each existing member to each other so BOTH sides send their join info
        // (replaces the direct PeerConnected trigger). The introduction travels over the campaign/relay
        // connection; the join info itself still flows over the IBattleNetwork mesh.
        foreach (var (otherControllerId, otherPeer) in others)
        {
            network.Send(otherPeer, new NetworkMissionPeerEntered(message.ControllerId, message.InstanceId));
            network.Send(peer, new NetworkMissionPeerEntered(otherControllerId, message.InstanceId));
        }
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
        // is NOT a retreat — the host adopts the dropped player's troops, so the reserve pointer is kept.
        messageBroker.Publish(this, new MissionMemberDeparted(
            controllerId,
            instanceId,
            wasRetreat: false,
            isInstanceEmpty: remaining.Count == 0));
    }
}
