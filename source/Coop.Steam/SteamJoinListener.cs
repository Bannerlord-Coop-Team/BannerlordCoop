using Common.Logging;
using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Serilog;
using System;

namespace Coop.Steam;

/// <summary>
/// Process-lifetime listener that turns Steam join requests (invite accepts, friends-list
/// "Join Game", +connect_lobby launches) into <see cref="SessionJoinInfoResolved"/> messages.
/// Lives outside any session container because invites arrive at the main menu, before a
/// session exists.
/// </summary>
public class SteamJoinListener : IDisposable, ISteamLobbyMembership
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamJoinListener>();

    private readonly IMessageBroker messageBroker;
    private readonly ISteamLobbyApi lobbyApi;

    private bool joinInFlight;
    private bool resolveJoinInfoAfterEnter;
    private bool leaveWhenJoinCompletes;
    private ulong joiningLobbyId;
    private ulong activeLobbyId;

    public ulong LobbyId => activeLobbyId;
    public bool IsInLobby => activeLobbyId != 0;

    public SteamJoinListener(IMessageBroker messageBroker, ISteamLobbyApi lobbyApi)
    {
        this.messageBroker = messageBroker;
        this.lobbyApi = lobbyApi;

        lobbyApi.LobbyJoinRequested += OnLobbyJoinRequested;
        lobbyApi.ConnectStringReceived += OnConnectStringReceived;

        messageBroker.Subscribe<JoinSteamLobby>(Handle);
    }

    /// <summary>
    /// Checks boot arguments for a +connect_lobby request: the OS command line (a cold
    /// invite launch appends it there) and Steam's launch command line (used instead when
    /// the app's partner config enables "Use launch command line").
    /// </summary>
    public void ProcessLaunchArguments(string commandLine)
    {
        try
        {
            OnConnectStringReceived(commandLine);
            OnConnectStringReceived(lobbyApi.GetLaunchCommandLine());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to process launch arguments");
        }
    }

    private void Handle(MessagePayload<JoinSteamLobby> payload)
    {
        OnLobbyJoinRequested(payload.What.LobbyId);
    }

    private void OnConnectStringReceived(string connectString)
    {
        if (!TryParseConnectLobby(connectString, out var lobbyId)) return;

        OnLobbyJoinRequested(lobbyId);
    }

    private void OnLobbyJoinRequested(ulong lobbyId)
    {
        BeginLobbyJoin(lobbyId, resolveJoinInfo: true);
    }

    public void JoinSessionLobby(ulong lobbyId)
    {
        BeginLobbyJoin(lobbyId, resolveJoinInfo: false);
    }

    private void BeginLobbyJoin(ulong lobbyId, bool resolveJoinInfo)
    {
        if (lobbyId == 0 || activeLobbyId == lobbyId) return;

        // A second JoinLobby would silently unregister the first pending call result.
        if (joinInFlight)
        {
            if (joiningLobbyId == lobbyId) return;

            Logger.Information("Ignoring Steam lobby join for {LobbyId}; another join is in flight", lobbyId.ToString());
            return;
        }

        LeaveActiveLobby();

        try
        {
            // Lobby ids are logged as strings; numeric log properties get double-rounded past 2^53 in structured viewers.
            Logger.Information("Joining Steam lobby {LobbyId}", lobbyId.ToString());
            joinInFlight = true;
            joiningLobbyId = lobbyId;
            resolveJoinInfoAfterEnter = resolveJoinInfo;
            leaveWhenJoinCompletes = false;
            lobbyApi.JoinLobby(lobbyId, OnLobbyEntered);
        }
        catch (Exception ex)
        {
            joinInFlight = false;
            joiningLobbyId = 0;
            resolveJoinInfoAfterEnter = false;
            Logger.Error(ex, "Failed to join Steam lobby {LobbyId}", lobbyId.ToString());
            if (resolveJoinInfo)
            {
                messageBroker.Publish(this, new SessionJoinFailed("Could not join the Steam lobby"));
            }
        }
    }

    private void OnLobbyEntered(ulong lobbyId, bool success)
    {
        joinInFlight = false;
        joiningLobbyId = 0;
        bool resolveJoinInfo = resolveJoinInfoAfterEnter;
        resolveJoinInfoAfterEnter = false;

        try
        {
            if (!success)
            {
                if (resolveJoinInfo)
                {
                    messageBroker.Publish(this, new SessionJoinFailed("Could not join the Steam lobby"));
                }
                return;
            }

            activeLobbyId = lobbyId;
            if (leaveWhenJoinCompletes)
            {
                leaveWhenJoinCompletes = false;
                LeaveActiveLobby();
                return;
            }

            if (!resolveJoinInfo)
            {
                Logger.Information("Joined session Steam lobby {LobbyId}", lobbyId.ToString());
                return;
            }

            bool decoded = LobbyDataCodec.TryDecode(key => lobbyApi.GetLobbyData(lobbyId, key), out var info, out var error);

            // A standalone server advertises its own game-server identity to tunnel to; otherwise the
            // lobby owner runs the tunnel. Either is only readable while still a member of the lobby.
            if (decoded && info.HasServerSteamId)
            {
                info.HostSteamId = info.ServerSteamId;
            }
            else if (decoded && info.Version >= SessionJoinInfo.MinTunnelVersion)
            {
                info.HostSteamId = lobbyApi.GetLobbyOwner(lobbyId);
            }

            if (!decoded)
            {
                LeaveActiveLobby();
                messageBroker.Publish(this, new SessionJoinFailed(error));
                return;
            }

            if (!info.HasAddress && !info.HasHostSteamId)
            {
                LeaveActiveLobby();
                messageBroker.Publish(this, new SessionJoinFailed(
                    "The host has not set a public address on their co-op screen, so this session cannot be joined yet"));
                return;
            }

            messageBroker.Publish(this, new SessionJoinInfoResolved(info));
        }
        catch (Exception ex)
        {
            LeaveActiveLobby();
            Logger.Error(ex, "Failed to read Steam lobby {LobbyId}", lobbyId.ToString());
            if (resolveJoinInfo)
            {
                messageBroker.Publish(this, new SessionJoinFailed("Could not read the Steam lobby"));
            }
        }
    }

    public void LeaveSessionLobby()
    {
        leaveWhenJoinCompletes = joinInFlight;
        LeaveActiveLobby();
    }

    private void LeaveActiveLobby()
    {
        if (activeLobbyId == 0) return;

        lobbyApi.LeaveLobby(activeLobbyId);
        activeLobbyId = 0;
    }

    public static bool TryParseConnectLobby(string text, out ulong lobbyId)
    {
        lobbyId = 0;

        if (string.IsNullOrEmpty(text)) return false;

        var tokens = text.Split(' ');
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            if (tokens[i].Equals(SteamLobbyAdvertiser.ConnectLobbyArgument, StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(tokens[i + 1], out lobbyId) && lobbyId != 0;
            }
        }

        return false;
    }

    public void Dispose()
    {
        LeaveSessionLobby();
        messageBroker.Unsubscribe<JoinSteamLobby>(Handle);
        lobbyApi.LobbyJoinRequested -= OnLobbyJoinRequested;
        lobbyApi.ConnectStringReceived -= OnConnectStringReceived;
    }
}
