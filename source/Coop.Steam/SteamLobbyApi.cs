using Common.Logging;
using Serilog;
using Steamworks;
using System;

namespace Coop.Steam;

/// <inheritdoc cref="ISteamLobbyApi"/>
/// <remarks>
/// Relies on the game's own Steam runtime: TaleWorlds initializes SteamAPI and pumps
/// SteamAPI.RunCallbacks every frame on the game thread, so callbacks registered here are
/// dispatched without a pump of our own. The dispatcher swallows handler exceptions, so
/// every callback body catches and logs its own failures.
/// </remarks>
public class SteamLobbyApi : ISteamLobbyApi
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamLobbyApi>();

    private readonly Callback<GameLobbyJoinRequested_t> lobbyJoinRequestedCallback;
    private readonly Callback<GameRichPresenceJoinRequested_t> richPresenceJoinRequestedCallback;
    private readonly Callback<NewUrlLaunchParameters_t> newLaunchParametersCallback;

    private CallResult<LobbyCreated_t> lobbyCreated;
    private CallResult<LobbyEnter_t> lobbyEntered;

    private bool createInFlight;
    private bool joinInFlight;

    private Action<ulong, bool> onCreateCompleted;
    private Action<ulong, bool> onJoinCompleted;

    public SteamLobbyApi()
    {
        lobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        richPresenceJoinRequestedCallback = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceJoinRequested);
        newLaunchParametersCallback = Callback<NewUrlLaunchParameters_t>.Create(OnNewUrlLaunchParameters);
    }

    public event Action<ulong> LobbyJoinRequested;
    public event Action<string> ConnectStringReceived;

    public bool IsOverlayEnabled
    {
        get
        {
            try
            {
                return SteamUtils.IsOverlayEnabled();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to query Steam overlay state");
                return false;
            }
        }
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        try
        {
            Logger.Information("Steam lobby join requested for lobby {LobbyId}", callback.m_steamIDLobby.m_SteamID);
            LobbyJoinRequested?.Invoke(callback.m_steamIDLobby.m_SteamID);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GameLobbyJoinRequested handler failed");
        }
    }

    private void OnGameRichPresenceJoinRequested(GameRichPresenceJoinRequested_t callback)
    {
        try
        {
            Logger.Information("Steam rich presence join requested: {Connect}", callback.m_rgchConnect);
            ConnectStringReceived?.Invoke(callback.m_rgchConnect);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GameRichPresenceJoinRequested handler failed");
        }
    }

    private void OnNewUrlLaunchParameters(NewUrlLaunchParameters_t callback)
    {
        try
        {
            var commandLine = GetLaunchCommandLine();
            Logger.Information("Steam launch parameters updated: {CommandLine}", commandLine);
            ConnectStringReceived?.Invoke(commandLine);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "NewUrlLaunchParameters handler failed");
        }
    }

    public void CreateFriendsOnlyLobby(int maxMembers, Action<ulong, bool> onCompleted)
    {
        onCreateCompleted = onCompleted;
        createInFlight = true;
        lobbyCreated ??= CallResult<LobbyCreated_t>.Create();

        var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
        lobbyCreated.Set(call, OnLobbyCreated);
    }

    private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
    {
        createInFlight = false;

        try
        {
            bool success = !ioFailure && result.m_eResult == EResult.k_EResultOK;
            if (!success)
            {
                Logger.Error("Steam lobby creation failed: ioFailure={IoFailure} result={Result}", ioFailure, result.m_eResult);
            }

            onCreateCompleted?.Invoke(result.m_ulSteamIDLobby, success);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LobbyCreated handler failed");
        }
    }

    public void JoinLobby(ulong lobbyId, Action<ulong, bool> onCompleted)
    {
        onJoinCompleted = onCompleted;
        joinInFlight = true;
        lobbyEntered ??= CallResult<LobbyEnter_t>.Create();

        var call = SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        lobbyEntered.Set(call, OnLobbyEntered);
    }

    private void OnLobbyEntered(LobbyEnter_t result, bool ioFailure)
    {
        joinInFlight = false;

        try
        {
            bool success = !ioFailure &&
                result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
            if (!success)
            {
                Logger.Error("Steam lobby join failed: ioFailure={IoFailure} response={Response}", ioFailure, result.m_EChatRoomEnterResponse);
            }

            onJoinCompleted?.Invoke(result.m_ulSteamIDLobby, success);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LobbyEnter handler failed");
        }
    }

    public void LeaveLobby(ulong lobbyId)
    {
        SteamMatchmaking.LeaveLobby(new CSteamID(lobbyId));
    }

    public bool SetLobbyData(ulong lobbyId, string key, string value)
    {
        return SteamMatchmaking.SetLobbyData(new CSteamID(lobbyId), key, value);
    }

    public string GetLobbyData(ulong lobbyId, string key)
    {
        return SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), key);
    }

    public void OpenInviteDialog(ulong lobbyId)
    {
        SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobbyId));
    }

    public bool SetRichPresenceConnect(string value)
    {
        return SteamFriends.SetRichPresence("connect", value);
    }

    public void ClearRichPresenceConnect()
    {
        // Setting a key to empty removes just that key; ClearRichPresence would wipe keys we don't own.
        SteamFriends.SetRichPresence("connect", string.Empty);
    }

    public string GetLaunchCommandLine()
    {
        SteamApps.GetLaunchCommandLine(out var commandLine, 1024);
        return commandLine ?? string.Empty;
    }

    public void Dispose()
    {
        lobbyJoinRequestedCallback?.Dispose();
        richPresenceJoinRequestedCallback?.Dispose();
        newLaunchParametersCallback?.Dispose();

        // A pending CallResult must survive dispose so the late completion still reaches its
        // handler (the advertiser leaves a lobby created after teardown); it unregisters itself
        // from the dispatcher once it fires.
        if (!createInFlight) lobbyCreated?.Dispose();
        if (!joinInFlight) lobbyEntered?.Dispose();
    }
}
