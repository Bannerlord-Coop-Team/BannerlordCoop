using Common;
using Common.Messaging;
using Common.Network.Session;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Session.Messages;
using Coop.Steam;
using System;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Keeps the local player in the server-owned Steam lobby for the network session.
/// </summary>
public class SessionLobbyMembershipHandler : IDisposable
{
    private readonly IMessageBroker messageBroker;
    private readonly ISessionAdvertiser sessionAdvertiser;
    private readonly ISteamLobbyMembership lobbyMembership;

    public SessionLobbyMembershipHandler(
        IMessageBroker messageBroker,
        ISessionAdvertiser sessionAdvertiser,
        ISteamLobbyMembership lobbyMembership)
    {
        this.messageBroker = messageBroker;
        this.sessionAdvertiser = sessionAdvertiser;
        this.lobbyMembership = lobbyMembership;

        messageBroker.Subscribe<NetworkSessionLobbyChanged>(Handle_LobbyChanged);
        messageBroker.Subscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    private void Handle_LobbyChanged(MessagePayload<NetworkSessionLobbyChanged> payload)
    {
        ulong lobbyId = payload.What.LobbyId;
        GameThread.RunSafe(() =>
        {
            // A Steam-capable server owns the canonical lobby; withdraw a temporary client lobby.
            sessionAdvertiser.StopAdvertising();
            lobbyMembership.JoinSessionLobby(lobbyId);
        },
            context: "JoinSessionSteamLobby");
    }

    private void Handle_NetworkDisconnected(MessagePayload<NetworkDisconnected> _)
    {
        GameThread.RunSafe(lobbyMembership.LeaveSessionLobby,
            context: "LeaveSessionSteamLobby");
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSessionLobbyChanged>(Handle_LobbyChanged);
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
        lobbyMembership.LeaveSessionLobby();
    }
}
