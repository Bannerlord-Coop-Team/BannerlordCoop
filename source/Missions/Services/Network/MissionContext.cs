using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Messages;
using LiteNetLib;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Services.Network;

/// <summary>
/// Lifetime only while in mission
/// </summary>
public interface IMissionContext : IDisposable
{
    IReadOnlyCollection<string> ControllersInMission { get; }
    void MapPeer(string controllerId, NetPeer peer);
    void RemovePeer(NetPeer peer);
    bool TryGetPeer(string controllerId, out NetPeer netPeer);

    /// <summary>
    /// Drop all mirrored membership and peer mappings for the mission we just left. The membership set
    /// mirrors server-announced instance membership and is otherwise pruned only per-controller as the
    /// server reports each departure — but once WE leave the instance the server stops fanning that
    /// instance's churn to us, so a controller that drops while we are away is never removed and keeps
    /// looking present. Call this at the local mission-exit chokepoint so the stale mirror does not survive
    /// into a later re-entry (BR-054), where it is rebuilt from the server's fresh membership re-announce.
    /// </summary>
    void EndInstance();
}

/// <summary>
/// Mirrors server-announced mission membership and maps verified direct peers to controller IDs,
/// excluding the local controller from <see cref="ControllersInMission"/>.
/// </summary>
public class MissionContext : IMissionContext, IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;


    private HashSet<string> controllersInMission = new();

    private readonly Dictionary<string, NetPeer> idToPeer = new();
    private readonly Dictionary<NetPeer, string> peerToId = new();

    private readonly object gate = new();

    public IReadOnlyCollection<string> ControllersInMission
    {
        get
        {
            lock (gate)
            {
                return controllersInMission
                    .Where(controllerId => controllerId != controllerIdProvider.ControllerId)
                    .ToArray();
            }
        }
    }

    public MissionContext(
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);

        messageBroker.Subscribe<PlayerLeftLocation>(Handle_PlayerLeftLocation);
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);

        messageBroker.Unsubscribe<PlayerLeftLocation>(Handle_PlayerLeftLocation);

        Clear();
    }

    private void Clear()
    {
        lock (gate)
        {
            controllersInMission.Clear();
            idToPeer.Clear();
            peerToId.Clear();
        }
    }

    // Leaving a mission wipes the whole mirror — same effect as leaving a location (Handle_PlayerLeftLocation),
    // exposed for the battle exit chokepoint (BattleInstanceLifecycle.Leave). A re-entry rebuilds it from the
    // server's membership re-announce, so this must not leave a stale controller behind.
    public void EndInstance() => Clear();

    public void MapPeer(string controllerId, NetPeer peer)
    {
        lock(gate)
        {
            // A rejoining controller can still have a stale mapping (its earlier leave/disconnect didn't clear
            // it, or it reconnected on a new NetPeer), so Dictionary.Add would throw "same key has already been
            // added" and kill the poll loop. Drop any prior entry for this controller OR this peer, then set —
            // making the mapping idempotent across rejoins.
            if (idToPeer.TryGetValue(controllerId, out var previousPeer))
                peerToId.Remove(previousPeer);
            if (peerToId.TryGetValue(peer, out var previousId))
                idToPeer.Remove(previousId);

            idToPeer[controllerId] = peer;
            peerToId[peer] = controllerId;
        }
    }

    public void RemovePeer(NetPeer peer)
    {
        lock(gate)
        {
            if (peerToId.TryGetValue(peer, out var controllerId))
            {
                peerToId.Remove(peer);
                idToPeer.Remove(controllerId);
            }
        }
    }

    public bool TryGetPeer(string controllerId, out NetPeer netPeer)
    {
        lock (gate)
        {
            return idToPeer.TryGetValue(controllerId, out netPeer);
        }
    }

    private void Handle_MissionPeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var controllerId = payload.What.ControllerId;
        lock (gate)
        {
            controllersInMission.Add(controllerId);
        }
    }

    private void Handle_MissionPeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        var controllerId = payload.What.ControllerId;
        lock (gate)
        {
            controllersInMission.Remove(controllerId);
            RemoveControllerMapping(controllerId);
        }
    }

    private void Handle_MissionPeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        var controllerId = payload.What.ControllerId;
        lock (gate)
        {
            controllersInMission.Remove(controllerId);
            RemoveControllerMapping(controllerId);
        }
    }

    private void RemoveControllerMapping(string controllerId)
    {
        if (!idToPeer.TryGetValue(controllerId, out var peer)) return;
        idToPeer.Remove(controllerId);
        peerToId.Remove(peer);
    }

    private void Handle_PlayerLeftLocation(MessagePayload<PlayerLeftLocation> payload)
    {
        Clear();
    }
}
