using Common.Messaging;
using Common.Network.Session;
using GameInterface.Services.Sessions.Messages;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Net;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Sessions;

/// <summary>
/// The public entry for a mod that runs its own mission over Coop with no campaign: host or join a session,
/// build the mission behaviors, follow the host and close the session.
/// </summary>
public interface ISessionLauncher : IDisposable
{
    /// <summary>Hosts a session. <paramref name="payload"/> is opaque to Coop and reaches every member.</summary>
    void Host(string modId, byte[] payload, string password, ServerVisibility visibility);

    /// <summary>Joins the session hosted at <paramref name="address"/>.</summary>
    void Join(IPAddress address, int port, string password);

    /// <summary>The behaviors the mod adds to its mission. Empty without a running Coop session.</summary>
    IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId);

    /// <summary>Asks the server to close the session. False unless this client is the host.</summary>
    bool Close();

    bool IsLocalHost { get; }

    ISessionModChannel Channel { get; }

    event Action<SessionHostChanged> HostChanged;
    event Action<SessionMemberJoined> MemberJoined;
    event Action<SessionMissionEndRequested> MissionEndRequested;
}

/// <inheritdoc cref="ISessionLauncher"/>
/// <remarks>
/// The message broker holds subscribers weakly, so the mod keeps the launcher in a field for as long as it uses it.
/// </remarks>
public sealed class SessionLauncher : ISessionLauncher
{
    private readonly Action<SessionMissionOpenRequested> openMission;
    private readonly IMessageBroker broker;
    private readonly ISessionModChannel channel = new ContainerSessionModChannel();

    /// <param name="openMission">Called on every peer once it joined: the mod opens its own mission from the payload.</param>
    public SessionLauncher(Action<SessionMissionOpenRequested> openMission) : this(openMission, MessageBroker.Instance)
    {
    }

    internal SessionLauncher(Action<SessionMissionOpenRequested> openMission, IMessageBroker broker)
    {
        this.openMission = openMission ?? throw new ArgumentNullException(nameof(openMission));
        this.broker = broker;

        broker.Subscribe<SessionMissionOpenRequested>(Handle_OpenRequested);
        broker.Subscribe<SessionHostChanged>(Handle_HostChanged);
        broker.Subscribe<SessionMemberJoined>(Handle_MemberJoined);
        broker.Subscribe<SessionMissionEndRequested>(Handle_EndRequested);
    }

    public event Action<SessionHostChanged> HostChanged;
    public event Action<SessionMemberJoined> MemberJoined;
    public event Action<SessionMissionEndRequested> MissionEndRequested;

    public ISessionModChannel Channel => channel;

    public bool IsLocalHost => ContainerProvider.TryResolve<ISessionMissionHost>(out var host) && host.IsLocalHost;

    public void Host(string modId, byte[] payload, string password, ServerVisibility visibility)
    {
        if (string.IsNullOrEmpty(modId)) throw new ArgumentException("A session needs a mod id", nameof(modId));
        if (payload != null && payload.Length > AttemptHostSession.MaxPayloadBytes)
            throw new ArgumentException($"The payload is over {AttemptHostSession.MaxPayloadBytes} bytes", nameof(payload));

        broker.Publish(this, new AttemptHostSession(modId, payload ?? new byte[0], password, visibility));
    }

    public void Join(IPAddress address, int port, string password)
    {
        broker.Publish(this, new AttemptJoin(address, port, password));
    }

    public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId)
    {
        if (!ContainerProvider.TryResolve<ISessionMissionHost>(out var host))
            return new MissionBehavior[0];

        return host.CreateMissionBehaviors(sessionId);
    }

    public bool Close() => ContainerProvider.TryResolve<ISessionMissionHost>(out var host) && host.TryRequestClose();

    public void Dispose()
    {
        broker.Unsubscribe<SessionMissionOpenRequested>(Handle_OpenRequested);
        broker.Unsubscribe<SessionHostChanged>(Handle_HostChanged);
        broker.Unsubscribe<SessionMemberJoined>(Handle_MemberJoined);
        broker.Unsubscribe<SessionMissionEndRequested>(Handle_EndRequested);
    }

    private void Handle_OpenRequested(MessagePayload<SessionMissionOpenRequested> payload) => openMission(payload.What);

    private void Handle_HostChanged(MessagePayload<SessionHostChanged> payload) => HostChanged?.Invoke(payload.What);

    private void Handle_MemberJoined(MessagePayload<SessionMemberJoined> payload) => MemberJoined?.Invoke(payload.What);

    private void Handle_EndRequested(MessagePayload<SessionMissionEndRequested> payload) => MissionEndRequested?.Invoke(payload.What);

    // Resolves the real channel per send, so a launcher built before Coop starts still works.
    private sealed class ContainerSessionModChannel : ISessionModChannel
    {
        public bool SendToSession(IMessage message) =>
            ContainerProvider.TryResolve<ISessionModChannel>(out var inner) && inner.SendToSession(message);

        public bool SendToController(string controllerId, IMessage message) =>
            ContainerProvider.TryResolve<ISessionModChannel>(out var inner) && inner.SendToController(controllerId, message);
    }
}
