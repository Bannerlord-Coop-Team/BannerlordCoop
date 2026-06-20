using Common.Messaging;
using GameInterface.Missions.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Missions.Services.Network;

/// <summary>
/// Lifetime only while in mission
/// </summary>
public interface IMissionContext : IDisposable
{
    IReadOnlyCollection<string> ControllersInMission { get; }
    void MapPeer(string controllerId, NetPeer peer);
    void RemovePeer(NetPeer peer);
    bool TryGetPeer(string controllerId, out NetPeer netPeer);
}

/// <summary>
/// Client-side mirror of the server's mission-instance membership. The server announces who is in the
/// instance over the campaign connection via <see cref="NetworkMissionPeerEntered"/> / <see cref="MissionPeerLeft"/>
/// / <see cref="MissionPeerDisconnected"/>; this tracks that set so <see cref="ControllersInMission"/> stays
/// equivalent to the server's view (minus the local controller).
/// </summary>
public class MissionContext : IMissionContext, IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;


    private HashSet<string> controllersInMission = new();

    private readonly Dictionary<string, NetPeer> idToPeer = new();
    private readonly Dictionary<NetPeer, string> peerToId = new();

    private readonly object gate = new();

    public IReadOnlyCollection<string> ControllersInMission =>
        controllersInMission
            .Where(controllerId => controllerId != controllerIdProvider.ControllerId)
            .ToArray();

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
        controllersInMission.Clear();
        idToPeer.Clear();
        peerToId.Clear();
    }

    public void MapPeer(string controllerId, NetPeer peer)
    {
        lock(gate)
        {
            idToPeer.Add(controllerId, peer);
            peerToId.Add(peer, controllerId);
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
        netPeer = null;

        return idToPeer.TryGetValue(controllerId, out netPeer);
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

    private void Handle_PlayerLeftLocation(MessagePayload<PlayerLeftLocation> payload)
    {
        Clear();
    }
}
