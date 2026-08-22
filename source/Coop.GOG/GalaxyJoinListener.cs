using Common.Logging;
using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Serilog;
using System;
using System.Globalization;

namespace Coop.GOG;

/// <summary>Turns Galaxy invite, rich-presence, and browser joins into resolved session info.</summary>
public sealed class GalaxyJoinListener : ISessionMembership, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<GalaxyJoinListener>();

    private readonly IMessageBroker messageBroker;
    private readonly IGalaxySdk sdk;
    private readonly ISessionJoinRequestGate joinRequestGate;
    private bool joinInFlight;
    private bool resolveJoinInfoAfterEnter;
    private bool leaveWhenJoinCompletes;
    private ulong joiningLobbyId;
    private ulong activeLobbyId;
    private ulong activeLobbyAdvertiserId;
    private bool disposed;

    internal GalaxyJoinListener(
        IMessageBroker messageBroker,
        IGalaxySdk sdk,
        ISessionJoinRequestGate joinRequestGate)
    {
        this.messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        this.sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
        this.joinRequestGate = joinRequestGate ?? throw new ArgumentNullException(nameof(joinRequestGate));

        sdk.GameJoinRequested += HandleGameJoinRequested;
        messageBroker.Subscribe<JoinSessionListing>(HandleJoinListing);
        messageBroker.Subscribe<SessionJoinAbandoned>(HandleJoinAbandoned);
    }

    public bool IsInSession => activeLobbyId != 0;
    public SessionListingId ListingId => activeLobbyId == 0
        ? default
        : new SessionListingId(
            GalaxySessionProvider.ProviderId,
            activeLobbyId.ToString(CultureInfo.InvariantCulture));

    private void HandleJoinListing(MessagePayload<JoinSessionListing> payload) =>
        BeginListingJoin(payload.What.ListingId, resolveJoinInfo: true);

    private void HandleJoinAbandoned(MessagePayload<SessionJoinAbandoned> payload) => LeaveSession();

    private void HandleGameJoinRequested(string connectionString)
    {
        if (TryParseConnectLobby(connectionString, out ulong lobbyId))
            BeginLobbyJoin(lobbyId, resolveJoinInfo: true);
    }

    public void JoinSession(SessionListingId listingId) =>
        BeginListingJoin(listingId, resolveJoinInfo: false);

    private void BeginListingJoin(SessionListingId listingId, bool resolveJoinInfo)
    {
        if (!string.Equals(listingId.Provider, GalaxySessionProvider.ProviderId, StringComparison.Ordinal) ||
            !ulong.TryParse(
                listingId.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong lobbyId))
        {
            return;
        }

        BeginLobbyJoin(lobbyId, resolveJoinInfo);
    }

    private void BeginLobbyJoin(ulong lobbyId, bool resolveJoinInfo)
    {
        if (lobbyId == 0) return;
        if (resolveJoinInfo && !joinRequestGate.CanStartJoin()) return;
        if (activeLobbyId == lobbyId)
        {
            if (resolveJoinInfo) ResolveJoinInfo(lobbyId);
            return;
        }

        if (joinInFlight)
        {
            if (joiningLobbyId != lobbyId)
                Logger.Information("Ignoring GOG lobby join; another join is in flight");
            return;
        }

        joinInFlight = true;
        joiningLobbyId = lobbyId;
        resolveJoinInfoAfterEnter = resolveJoinInfo;
        leaveWhenJoinCompletes = false;
        try
        {
            sdk.EnsureAuthenticated(authenticated =>
                HandleAuthenticationCompleted(lobbyId, authenticated));
        }
        catch (Exception ex)
        {
            CompleteJoinFailure(
                lobbyId,
                "Could not authenticate with GOG Galaxy",
                ex);
        }
    }

    private void HandleAuthenticationCompleted(ulong lobbyId, bool authenticated)
    {
        if (disposed || !joinInFlight || joiningLobbyId != lobbyId) return;
        if (leaveWhenJoinCompletes)
        {
            ResetJoinState();
            return;
        }

        if (!authenticated)
        {
            CompleteJoinFailure(
                lobbyId,
                "GOG Galaxy is not signed in; launch Bannerlord through GOG Galaxy and try again");
            return;
        }

        if (resolveJoinInfoAfterEnter && !joinRequestGate.CanStartJoin())
        {
            ResetJoinState();
            return;
        }

        try
        {
            LeaveActiveLobby();
            sdk.JoinLobby(lobbyId, HandleLobbyEntered);
        }
        catch (Exception ex)
        {
            CompleteJoinFailure(lobbyId, "Could not join the GOG lobby", ex);
        }
    }

    private void CompleteJoinFailure(ulong lobbyId, string reason, Exception exception = null)
    {
        bool resolveJoinInfo = resolveJoinInfoAfterEnter;
        ResetJoinState();
        if (exception != null)
            Logger.Error(exception, "Failed to join GOG lobby {LobbyId}", lobbyId.ToString());
        if (resolveJoinInfo) messageBroker.Publish(this, new SessionJoinFailed(reason));
    }

    private void ResetJoinState()
    {
        joinInFlight = false;
        joiningLobbyId = 0;
        resolveJoinInfoAfterEnter = false;
        leaveWhenJoinCompletes = false;
    }

    private void HandleLobbyEntered(ulong lobbyId, bool success)
    {
        joinInFlight = false;
        joiningLobbyId = 0;
        bool resolveJoinInfo = resolveJoinInfoAfterEnter;
        resolveJoinInfoAfterEnter = false;

        if (!success)
        {
            leaveWhenJoinCompletes = false;
            if (resolveJoinInfo)
                messageBroker.Publish(this, new SessionJoinFailed("Could not join the GOG lobby"));
            return;
        }

        try
        {
            activeLobbyId = lobbyId;
            activeLobbyAdvertiserId = sdk.GetLobbyOwner(lobbyId);
            if (leaveWhenJoinCompletes)
            {
                leaveWhenJoinCompletes = false;
                LeaveActiveLobby();
                return;
            }

            if (resolveJoinInfo) ResolveJoinInfo(lobbyId);
        }
        catch (Exception ex)
        {
            LeaveActiveLobby();
            Logger.Error(ex, "Failed to read GOG lobby {LobbyId}", lobbyId.ToString());
            if (resolveJoinInfo)
                messageBroker.Publish(this, new SessionJoinFailed("Could not read the GOG lobby"));
        }
    }

    private void ResolveJoinInfo(ulong lobbyId)
    {
        try
        {
            bool decoded = SessionListingDataCodec.TryDecode(
                key => sdk.GetLobbyData(lobbyId, key),
                out var info,
                out string error);
            if (!decoded)
            {
                LeaveActiveLobby();
                messageBroker.Publish(this, new SessionJoinFailed(error));
                return;
            }

            if (info.HasTunnelTarget &&
                info.TunnelTarget != GalaxyDatagramTransport.GalaxyIdentity(activeLobbyAdvertiserId))
            {
                LeaveActiveLobby();
                messageBroker.Publish(this, new SessionJoinFailed(
                    "The GOG lobby advertised a networking identity that does not match its owner"));
                return;
            }

            if (!info.HasAddress && !info.HasTunnelTarget)
            {
                LeaveActiveLobby();
                messageBroker.Publish(this, new SessionJoinFailed(
                    "The host has not shared a reachable co-op endpoint"));
                return;
            }

            messageBroker.Publish(this, new SessionJoinInfoResolved(info));
        }
        catch (Exception ex)
        {
            LeaveActiveLobby();
            Logger.Error(ex, "Failed to resolve GOG lobby {LobbyId}", lobbyId.ToString());
            messageBroker.Publish(this, new SessionJoinFailed("Could not read the GOG lobby"));
        }
    }

    public void LeaveSession()
    {
        leaveWhenJoinCompletes = joinInFlight;
        LeaveActiveLobby();
    }

    private void LeaveActiveLobby()
    {
        if (activeLobbyId == 0) return;

        if (activeLobbyAdvertiserId != 0 && activeLobbyAdvertiserId == sdk.LocalUserId)
        {
            Logger.Information("Staying in own GOG lobby {LobbyId}; the advertisement needs this account's membership",
                activeLobbyId.ToString());
        }
        else
        {
            sdk.LeaveLobby(activeLobbyId);
        }

        activeLobbyId = 0;
        activeLobbyAdvertiserId = 0;
    }

    internal static bool TryParseConnectLobby(string text, out ulong lobbyId)
    {
        lobbyId = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < tokens.Length - 1; index++)
        {
            if (tokens[index].Equals(
                    GalaxyLobbyAdvertiser.ConnectLobbyArgument,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(
                    tokens[index + 1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out lobbyId) && lobbyId != 0;
            }
        }

        return false;
    }

    public void Dispose()
    {
        disposed = true;
        LeaveSession();
        sdk.GameJoinRequested -= HandleGameJoinRequested;
        messageBroker.Unsubscribe<JoinSessionListing>(HandleJoinListing);
        messageBroker.Unsubscribe<SessionJoinAbandoned>(HandleJoinAbandoned);
    }
}
