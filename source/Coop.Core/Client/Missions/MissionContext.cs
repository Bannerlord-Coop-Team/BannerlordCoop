using Common.Messaging;
using Coop.Core.Client.Missions.Messages;
using GameInterface.Services.Entity;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Coop.Core.Client.Missions;

/// <summary>
/// Lifetime only while in mission
/// </summary>
public interface IMissionContext : IDisposable
{
    public IReadOnlyCollection<string> ControllersInMission { get; }
    void MapPeer(string controllerId, NetPeer peer);
    void RemovePeer(NetPeer peer);
    bool TryGetPeer(string controllerId, out NetPeer netPeer);
}

internal class MissionContext : IMissionContext, IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;

    private ImmutableHashSet<string> controllersInMission = ImmutableHashSet<string>.Empty;

    private readonly ConcurrentDictionary<string, NetPeer> idToPeer = new();
    private readonly ConcurrentDictionary<NetPeer, string> peerToId = new();

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

        messageBroker.Subscribe<NetworkControllerEnteredMission>(Handle_NetworkControllerEnteredMission);
        messageBroker.Subscribe<NetworkControllerLeftMission>(Handle_NetworkControllerLeftMission);
    }

    public void Dispose()
    {
        messageBroker.Subscribe<NetworkControllerEnteredMission>(Handle_NetworkControllerEnteredMission);
        messageBroker.Subscribe<NetworkControllerLeftMission>(Handle_NetworkControllerLeftMission);
    }

    public void MapPeer(string controllerId, NetPeer peer)
    {
        idToPeer.TryAdd(controllerId, peer);
        peerToId.TryAdd(peer, controllerId);
    }

    public void RemovePeer(NetPeer peer)
    {
        if (peerToId.TryGetValue(peer, out var controllerId))
        {
            idToPeer.TryRemove(controllerId, out var _);
        }
    }

    public bool TryGetPeer(string controllerId, out NetPeer netPeer)
    {
        netPeer = null;

        return idToPeer.TryGetValue(controllerId, out netPeer);
    }

    private void Handle_NetworkControllerEnteredMission(
        MessagePayload<NetworkControllerEnteredMission> payload)
    {
        ImmutableInterlocked.Update(
            ref controllersInMission,
            controllers => controllers.Add(payload.What.ControllerId));
    }

    private void Handle_NetworkControllerLeftMission(
        MessagePayload<NetworkControllerLeftMission> payload)
    {
        ImmutableInterlocked.Update(
            ref controllersInMission,
            controllers => controllers.Remove(payload.What.ControllerId));
    }
}
