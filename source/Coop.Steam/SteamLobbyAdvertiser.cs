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

    private readonly ISteamLobbyApi lobbyApi;

    private ulong lobbyId;
    private bool createInFlight;
    private bool disposed;
    private SessionJoinInfo pendingInfo;

    public SteamLobbyAdvertiser(ISteamLobbyApi lobbyApi)
    {
        this.lobbyApi = lobbyApi;
    }

    public bool IsAdvertising => lobbyId != 0;

    public void Advertise(SessionJoinInfo info)
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
        lobbyApi.CreateFriendsOnlyLobby(MaxLobbyMembers, OnLobbyCreated);
    }

    private void OnLobbyCreated(ulong createdLobbyId, bool success)
    {
        createInFlight = false;

        if (!success)
        {
            Logger.Error("Could not create a Steam lobby; invites are unavailable this session");
            return;
        }

        // A stop or dispose can arrive while creation is in flight; don't resurrect the lobby.
        if (disposed || pendingInfo == null)
        {
            lobbyApi.LeaveLobby(createdLobbyId);
            return;
        }

        lobbyId = createdLobbyId;
        Logger.Information("Steam lobby {LobbyId} created", lobbyId);
        ApplyLobbyData();
    }

    private void ApplyLobbyData()
    {
        bool applied = true;
        foreach (var pair in LobbyDataCodec.Encode(pendingInfo))
        {
            applied &= lobbyApi.SetLobbyData(lobbyId, pair.Key, pair.Value);
        }

        // Advertising a lobby with partial metadata just strands invitees; withdraw instead.
        if (!applied)
        {
            Logger.Error("Steam lobby data writes failed; withdrawing the advertisement");
            StopAdvertising();
            return;
        }

        lobbyApi.SetRichPresenceConnect($"{ConnectLobbyArgument} {lobbyId}");
    }

    public void StopAdvertising()
    {
        pendingInfo = null;

        if (lobbyId != 0)
        {
            lobbyApi.LeaveLobby(lobbyId);
            lobbyId = 0;
        }

        lobbyApi.ClearRichPresenceConnect();
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

    public void Dispose()
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
