using Common;
using Common.Messaging;
using Common.Network.Session;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Session.Messages;
using System;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Keeps the local player in the server-owned provider listing for the network session.
/// </summary>
public class SessionLobbyMembershipHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ISessionAdvertiser sessionAdvertiser;
    private readonly ISessionMembership sessionMembership;

    public SessionLobbyMembershipHandler(
        IMessageBroker messageBroker,
        ISessionAdvertiser sessionAdvertiser,
        ISessionMembership sessionMembership)
    {
        this.messageBroker = messageBroker;
        this.sessionAdvertiser = sessionAdvertiser;
        this.sessionMembership = sessionMembership;

        messageBroker.Subscribe<NetworkSessionLobbyChanged>(Handle_LobbyChanged);
        messageBroker.Subscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    private void Handle_LobbyChanged(MessagePayload<NetworkSessionLobbyChanged> payload)
    {
        SessionListingId listingId = payload.What.ToListingId();
        if (!listingId.IsValid) return;

        GameThread.RunSafe(() =>
        {
            // The server owns the canonical listing; withdraw any temporary client advertisement.
            sessionAdvertiser.StopAdvertising();
            sessionMembership.JoinSession(listingId);
        },
            context: "JoinProviderSession");
    }

    private void Handle_NetworkDisconnected(MessagePayload<NetworkDisconnected> _)
    {
        GameThread.RunSafe(sessionMembership.LeaveSession,
            context: "LeaveProviderSession");
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSessionLobbyChanged>(Handle_LobbyChanged);
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
        sessionMembership.LeaveSession();
    }
}
