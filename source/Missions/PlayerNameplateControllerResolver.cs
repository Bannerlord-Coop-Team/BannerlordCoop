using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.UI.PlayerNameplates;
using Missions.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions;

/// <summary>Resolves the remote controller currently driving a mission agent.</summary>
public sealed class PlayerNameplateControllerResolver : IPlayerNameplateControllerResolver
{
    private readonly object gate = new object();
    private readonly Dictionary<string, FocusEntry> controllerFocus = new Dictionary<string, FocusEntry>();
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IMessageBroker messageBroker;
    private readonly IControllerIdProvider controllerIdProvider;
    private bool disposed;

    public PlayerNameplateControllerResolver(
        INetworkAgentRegistry agentRegistry,
        IMessageBroker messageBroker,
        IControllerIdProvider controllerIdProvider)
    {
        this.agentRegistry = agentRegistry;
        this.messageBroker = messageBroker;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<NetworkMovementReceiverCap>(HandleReceiverCap);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(HandlePeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(HandlePeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(HandlePeerDisconnected);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            controllerFocus.Clear();
        }

        messageBroker.Unsubscribe<NetworkMovementReceiverCap>(HandleReceiverCap);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(HandlePeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(HandlePeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(HandlePeerDisconnected);
    }

    public bool TryGetControllerId(Agent agent, out string controllerId)
    {
        controllerId = null;
        if (!agentRegistry.TryGetAgentInfo(agent, out var agentInfo)) return false;

        string currentAuthority = agentInfo.CurrentAuthority;
        if (string.IsNullOrEmpty(currentAuthority) || currentAuthority == controllerIdProvider.ControllerId)
            return false;

        lock (gate)
        {
            if (disposed ||
                !controllerFocus.TryGetValue(currentAuthority, out var focus) ||
                focus.AgentId != agentInfo.AgentId)
                return false;
        }

        controllerId = currentAuthority;
        return true;
    }

    private void HandleReceiverCap(MessagePayload<NetworkMovementReceiverCap> payload)
    {
        var message = payload.What;
        if (string.IsNullOrEmpty(message.ControllerId) ||
            message.ControllerId == controllerIdProvider.ControllerId)
            return;

        lock (gate)
        {
            if (disposed ||
                (controllerFocus.TryGetValue(message.ControllerId, out var existing) &&
                 message.Sequence <= existing.Sequence))
                return;

            controllerFocus[message.ControllerId] = new FocusEntry(message.FocusAgentId, message.Sequence);
        }
    }

    private void HandlePeerEntered(MessagePayload<NetworkMissionPeerEntered> payload) =>
        RemoveController(payload.What.ControllerId);

    private void HandlePeerLeft(MessagePayload<MissionPeerLeft> payload) =>
        RemoveController(payload.What.ControllerId);

    private void HandlePeerDisconnected(MessagePayload<MissionPeerDisconnected> payload) =>
        RemoveController(payload.What.ControllerId);

    private void RemoveController(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        lock (gate)
        {
            if (!disposed)
                controllerFocus.Remove(controllerId);
        }
    }

    private readonly struct FocusEntry
    {
        public readonly Guid AgentId;
        public readonly long Sequence;

        public FocusEntry(Guid agentId, long sequence)
        {
            AgentId = agentId;
            Sequence = sequence;
        }
    }
}
