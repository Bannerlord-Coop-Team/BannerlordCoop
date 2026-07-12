using Common.Logging;
using Common.Network.Session;
using Serilog;
using System;

namespace Coop.Steam;

/// <summary>
/// Advertises the session as a friends-only Steam lobby carrying the join info, and sets
/// rich presence so friends get "Join Game" in their Steam friends list.
/// </summary>
public class SteamLobbyAdvertiser : ISessionAdvertiser
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamLobbyAdvertiser>();

    public const int MaxLobbyMembers = 16;
    public const string ConnectLobbyArgument = "+connect_lobby";

    protected readonly ISteamLobbyApi lobbyApi;

    private ulong lobbyId;
    private bool createInFlight;
    private bool disposed;
    private bool richPresenceSet;
    private SessionJoinInfo pendingInfo;

    public SteamLobbyAdvertiser(ISteamLobbyApi lobbyApi)
    {
        this.lobbyApi = lobbyApi;
    }

    public bool IsAdvertising => lobbyId != 0;

    public virtual void Advertise(SessionJoinInfo info)
    {
        if (disposed) return;

        pendingInfo = info;

        if (lobbyId != 0)
        {
            ApplyLobbyData();
            return;
        }

        if (createInFlight) return;

        createInFlight = true;
        try
        {
            RequestLobby(MaxLobbyMembers, OnLobbyCreated);
        }
        catch (Exception ex)
        {
            createInFlight = false;
            Logger.Error(ex, "Could not request a Steam lobby");
            OnLobbyUnavailable(pendingInfo);
        }
    }

    // The standalone-server advertiser overrides this to create a browsable public lobby instead.
    protected virtual void RequestLobby(int maxMembers, Action<ulong, bool> onCompleted)
        => lobbyApi.CreateFriendsOnlyLobby(maxMembers, onCompleted);

    private void OnLobbyCreated(ulong createdLobbyId, bool success)
    {
        createInFlight = false;

        if (!success)
        {
            Logger.Error("Could not create a Steam lobby; invites are unavailable this session");
            OnLobbyUnavailable(pendingInfo);
            return;
        }

        // A stop or dispose can arrive while creation is in flight; don't resurrect the lobby.
        if (disposed || pendingInfo == null)
        {
            lobbyApi.LeaveLobby(createdLobbyId);
            return;
        }

        lobbyId = createdLobbyId;
        // Lobby ids are logged as strings; numeric log properties get double-rounded past 2^53 in structured viewers.
        Logger.Information("Steam lobby {LobbyId} created", lobbyId.ToString());
        ApplyLobbyData();
    }

    private void ApplyLobbyData()
    {
        bool applied = true;
        try
        {
            foreach (var pair in LobbyDataCodec.Encode(pendingInfo))
            {
                applied &= lobbyApi.SetLobbyData(lobbyId, pair.Key, pair.Value);
            }
        }
        catch (Exception ex)
        {
            applied = false;
            Logger.Error(ex, "Steam lobby data writes threw an exception");
        }

        // Advertising a lobby with partial metadata just strands invitees; withdraw instead.
        if (!applied)
        {
            Logger.Error("Steam lobby data writes failed; withdrawing the advertisement");
            var failedInfo = pendingInfo;
            WithdrawAdvertisement();
            OnLobbyUnavailable(failedInfo);
            return;
        }

        try
        {
            richPresenceSet |= lobbyApi.SetRichPresenceConnect($"{ConnectLobbyArgument} {lobbyId}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not set Steam rich presence for the lobby");
        }
    }

    protected virtual void OnLobbyUnavailable(SessionJoinInfo info)
    {
    }

    public virtual void StopAdvertising()
    {
        WithdrawAdvertisement();
    }

    private void WithdrawAdvertisement()
    {
        pendingInfo = null;

        if (lobbyId != 0)
        {
            try
            {
                lobbyApi.LeaveLobby(lobbyId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not leave Steam lobby {LobbyId}", lobbyId.ToString());
            }
            lobbyId = 0;
        }

        if (richPresenceSet)
        {
            try
            {
                lobbyApi.ClearRichPresenceConnect();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not clear Steam lobby rich presence");
            }
            richPresenceSet = false;
        }
    }

    public bool InviteFriends()
    {
        if (lobbyId == 0) return false;

        if (!lobbyApi.IsOverlayEnabled)
        {
            Logger.Warning("Steam overlay unavailable; friends can still join from the Steam friends list");
            return false;
        }

        lobbyApi.OpenInviteDialog(lobbyId);
        return true;
    }

    public virtual void Dispose()
    {
        disposed = true;

        try
        {
            StopAdvertising();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to stop advertising during dispose");
        }
    }
}
