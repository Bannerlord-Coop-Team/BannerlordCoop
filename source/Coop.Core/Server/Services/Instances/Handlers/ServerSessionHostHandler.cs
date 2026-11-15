using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Instances.Sessions;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using Missions.Sessions;
using Missions.Sessions.Messages;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// [Server] Elects the host of a campaign-free session, keeps the successor line, and closes the session on
/// the host's request. The first member to report mission ready is the host; a departure promotes the next
/// successor. Every message to a member goes out per peer: a session client never transfers a save, so the
/// connection queue drops a <c>SendAll</c> for it.
/// </summary>
internal class ServerSessionHostHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSessionHostHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IMissionManager missionManager;
    private readonly IPlayerManager playerManager;
    private readonly ISessionHostRegistry hostRegistry;
    private readonly ISessionDefinitionStore definitions;
    private readonly object gate = new object();

    public ServerSessionHostHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IMissionManager missionManager,
        IPlayerManager playerManager,
        ISessionHostRegistry hostRegistry,
        ISessionDefinitionStore definitions)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.missionManager = missionManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.definitions = definitions;

        messageBroker.Subscribe<NetworkRequestSessionHost>(Handle_RequestSessionHost);
        messageBroker.Subscribe<NetworkRequestSessionClose>(Handle_RequestSessionClose);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MemberDeparted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestSessionHost>(Handle_RequestSessionHost);
        messageBroker.Unsubscribe<NetworkRequestSessionClose>(Handle_RequestSessionClose);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MemberDeparted);
    }

    private void Handle_RequestSessionHost(MessagePayload<NetworkRequestSessionHost> payload)
    {
        if (payload.Who is not NetPeer peer)
            return;
        if (!playerManager.TryGetCurrentController(peer, out var controllerId))
            return;

        var sessionId = payload.What.SessionId;
        lock (gate)
        {
            if (!SessionInstanceId.IsSession(sessionId) || definitions.Current?.SessionId != sessionId)
                return;

            if (!missionManager.TryGetControllers(sessionId, out var members) || !members.Contains(controllerId))
            {
                Logger.Debug("Ignoring session host request from {Controller}: not a member of {Session}", controllerId, sessionId);
                return;
            }

            if (!hostRegistry.TryGet(sessionId, out var current))
            {
                var elected = new SessionHostAssignment(controllerId, new string[0], hostRegistry.NextEpoch(sessionId));
                hostRegistry.Set(sessionId, elected);
                Logger.Information("Elected {Host} as host of session {Session} at epoch {Epoch}", controllerId, sessionId, elected.Epoch);
                SendToMembers(sessionId, members, elected);
                return;
            }

            if (current.HostControllerId == controllerId || current.SuccessorControllerIds.Contains(controllerId))
            {
                network.Send(peer, ToMessage(sessionId, current));
                return;
            }

            var successors = current.SuccessorControllerIds.Concat(new[] { controllerId }).ToArray();
            var appended = new SessionHostAssignment(current.HostControllerId, successors, current.Epoch);
            hostRegistry.Set(sessionId, appended);
            SendToMembers(sessionId, members, appended);
        }
    }

    private void Handle_MemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        var departure = payload.What;
        lock (gate)
        {
            if (!hostRegistry.TryGet(departure.InstanceId, out var current))
                return;

            if (departure.IsInstanceEmpty)
            {
                // The session stays open: the next member to report ready is elected at a higher epoch.
                hostRegistry.Remove(departure.InstanceId);
                return;
            }

            var successors = current.SuccessorControllerIds.ToList();
            SessionHostAssignment updated;
            if (current.HostControllerId == departure.ControllerId)
            {
                if (successors.Count == 0)
                {
                    hostRegistry.Remove(departure.InstanceId);
                    return;
                }

                var promoted = successors[0];
                successors.RemoveAt(0);
                updated = new SessionHostAssignment(promoted, successors, hostRegistry.NextEpoch(departure.InstanceId, current.Epoch));
                Logger.Information("Promoted {Host} to host of session {Session} at epoch {Epoch}", promoted, departure.InstanceId, updated.Epoch);
            }
            else if (successors.Remove(departure.ControllerId))
            {
                updated = new SessionHostAssignment(current.HostControllerId, successors, current.Epoch);
            }
            else
            {
                return;
            }

            hostRegistry.Set(departure.InstanceId, updated);
            if (missionManager.TryGetControllers(departure.InstanceId, out var members))
                SendToMembers(departure.InstanceId, members, updated);
        }
    }

    private void Handle_RequestSessionClose(MessagePayload<NetworkRequestSessionClose> payload)
    {
        if (payload.Who is not NetPeer peer)
            return;
        if (!playerManager.TryGetCurrentController(peer, out var controllerId))
            return;

        var request = payload.What;
        lock (gate)
        {
            if (!hostRegistry.TryGet(request.SessionId, out var current)
                || current.HostControllerId != controllerId
                || current.Epoch != request.HostEpoch)
            {
                Logger.Warning("Ignoring close of session {Session} from {Controller}: not the host at epoch {Epoch}",
                    request.SessionId, controllerId, request.HostEpoch);
                return;
            }

            if (!missionManager.TryGetControllers(request.SessionId, out var members)
                || !missionManager.TryBeginActiveInstanceConclusion(request.SessionId, members))
                return;

            foreach (var member in members)
            {
                if (playerManager.TryGetPeer(member, out var memberPeer))
                    network.Send(memberPeer, new NetworkEndBattleMission(request.SessionId));
            }

            missionManager.CompleteInstanceConclusion(request.SessionId, true);
            hostRegistry.Forget(request.SessionId);
            definitions.Close(request.SessionId);
            Logger.Information("Closed session {Session}", request.SessionId);
            messageBroker.Publish(this, new SessionClosed(request.SessionId));
        }
    }

    private void SendToMembers(string sessionId, IReadOnlyCollection<string> members, SessionHostAssignment assignment)
    {
        foreach (var member in members)
        {
            if (playerManager.TryGetPeer(member, out var memberPeer))
                network.Send(memberPeer, ToMessage(sessionId, assignment));
        }
    }

    private static NetworkBattleHostAssigned ToMessage(string sessionId, SessionHostAssignment assignment) =>
        new NetworkBattleHostAssigned(sessionId, assignment.HostControllerId, assignment.SuccessorControllerIds.ToArray(), assignment.Epoch);
}
