using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Messages;
using Serilog;
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
    /// Scope the membership mirror to <paramref name="instanceId"/>: introductions for any other instance are
    /// ignored from here on, and membership left over from a previous instance is dropped. Called when the
    /// mission connects to its instance (<see cref="LiteNetP2PClient.ConnectToInstance"/>), before the entry
    /// is announced to the server — so every introduction the announce triggers is accepted. Re-invoking with
    /// the current instance id is a no-op (entry handlers can fire more than once per mission).
    /// </summary>
    void BeginInstance(string instanceId);

    /// <summary>
    /// Leave the current instance: drop all membership and peer mappings and ignore introductions until the
    /// next <see cref="BeginInstance"/>. Called from the mission's network teardown
    /// (<see cref="LiteNetP2PClient.DisconnectPeers"/>), after the departure was announced to the server.
    /// </summary>
    void EndInstance();
}

/// <summary>
/// Client-side mirror of the server's mission-instance membership. The server announces who is in the
/// instance over the campaign connection via <see cref="NetworkMissionPeerEntered"/> / <see cref="MissionPeerLeft"/>
/// / <see cref="MissionPeerDisconnected"/>; this tracks that set so <see cref="ControllersInMission"/> stays
/// equivalent to the server's view (minus the local controller).
/// <para>
/// This object lives for the whole client session while missions come and go, so membership is scoped to the
/// instance begun via <see cref="BeginInstance"/> and dropped on <see cref="EndInstance"/>. Without that scoping,
/// members of a finished battle linger here (the server only fans a departure out to members still in its table,
/// so whoever leaves first never hears about the later leavers) and every broadcast of the NEXT mission is
/// relayed at them — which the server can only fail with "Failed to get peer for instance".
/// </para>
/// </summary>
public class MissionContext : IMissionContext, IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionContext>();

    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;


    private HashSet<string> controllersInMission = new();

    private readonly Dictionary<string, NetPeer> idToPeer = new();
    private readonly Dictionary<NetPeer, string> peerToId = new();

    // The instance the membership above belongs to; null while not in an instance. Guarded by gate.
    private string currentInstanceId;

    private readonly object gate = new();

    public IReadOnlyCollection<string> ControllersInMission
    {
        get
        {
            // Snapshot under the lock: the movement poll enumerates this on the game thread while the
            // network thread adds/removes members, and an unguarded HashSet enumeration can throw or tear.
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
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_MissionPeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);

        lock (gate)
        {
            Clear();
        }
    }

    public void BeginInstance(string instanceId)
    {
        lock (gate)
        {
            // Entry handlers can run more than once per mission; a repeat for the SAME instance must not
            // wipe the membership already gathered.
            if (currentInstanceId == instanceId) return;

            currentInstanceId = instanceId;
            Clear();
        }
    }

    public void EndInstance()
    {
        lock (gate)
        {
            currentInstanceId = null;
            Clear();
        }
    }

    // Caller holds the lock.
    private void Clear()
    {
        controllersInMission.Clear();
        idToPeer.Clear();
        peerToId.Clear();
    }

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
            // Only mirror members of OUR instance. A mismatch is a stale or in-flight introduction (e.g. one
            // sent for the previous instance just as we left it); accepting it would make every broadcast of
            // this mission relay at a controller the server rightly has no mapping for here. A null instance id
            // is tolerated as a wildcard, matching Handle_PeerLeft in BattleAuthorityMigrator (only locally
            // published legacy/test messages omit it — the server fan-out always carries the id).
            if (payload.What.InstanceId != null && payload.What.InstanceId != currentInstanceId)
            {
                Logger.Debug("Ignoring introduction of {Controller} for instance {Instance} — current instance is {Current}",
                    controllerId, payload.What.InstanceId, currentInstanceId ?? "<none>");
                return;
            }

            controllersInMission.Add(controllerId);
        }
    }

    private void Handle_MissionPeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        // Departures are deliberately NOT filtered by instance: removing a controller that is not in the set
        // is a no-op, and an unfiltered removal heals a stale entry no matter which instance it leaked from.
        var controllerId = payload.What.ControllerId;
        lock (gate)
        {
            controllersInMission.Remove(controllerId);
        }
    }

    private void Handle_MissionPeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        var controllerId = payload.What.ControllerId;
        lock (gate)
        {
            controllersInMission.Remove(controllerId);
        }
    }
}
