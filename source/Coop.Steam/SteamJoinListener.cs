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
public class SteamJoinListener : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamJoinListener>();

    private readonly IMessageBroker messageBroker;
    private readonly ISteamLobbyApi lobbyApi;

    private bool joinInFlight;

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
        // A second JoinLobby would silently unregister the first pending call result.
        if (joinInFlight)
        {
            Logger.Information("Ignoring Steam lobby join for {LobbyId}; another join is in flight", lobbyId.ToString());
            return;
        }

        try
        {
            // Lobby ids are logged as strings; numeric log properties get double-rounded past 2^53 in structured viewers.
            Logger.Information("Joining Steam lobby {LobbyId}", lobbyId.ToString());
            joinInFlight = true;
            lobbyApi.JoinLobby(lobbyId, OnLobbyEntered);
        }
        catch (Exception ex)
        {
            joinInFlight = false;
            Logger.Error(ex, "Failed to join Steam lobby {LobbyId}", lobbyId.ToString());
            messageBroker.Publish(this, new SessionJoinFailed("Could not join the Steam lobby"));
        }
    }

    private void OnLobbyEntered(ulong lobbyId, bool success)
    {
        joinInFlight = false;

        try
        {
            if (!success)
            {
                messageBroker.Publish(this, new SessionJoinFailed("Could not join the Steam lobby"));
                return;
            }

            // Membership is only needed to read the join info; leaving keeps the host's slots free.
            bool decoded = LobbyDataCodec.TryDecode(key => lobbyApi.GetLobbyData(lobbyId, key), out var info, out var error);
            lobbyApi.LeaveLobby(lobbyId);

            if (!decoded)
            {
                messageBroker.Publish(this, new SessionJoinFailed(error));
                return;
            }

            if (!info.HasAddress)
            {
                messageBroker.Publish(this, new SessionJoinFailed(
                    "The host has not set a public address on their co-op screen, so this session cannot be joined yet"));
                return;
            }

            messageBroker.Publish(this, new SessionJoinInfoResolved(info));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read Steam lobby {LobbyId}", lobbyId.ToString());
            messageBroker.Publish(this, new SessionJoinFailed("Could not read the Steam lobby"));
        }
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
        messageBroker.Unsubscribe<JoinSteamLobby>(Handle);
        lobbyApi.LobbyJoinRequested -= OnLobbyJoinRequested;
        lobbyApi.ConnectStringReceived -= OnConnectStringReceived;
    }
}
