using Common.Logging;
using Serilog;
using Steamworks;
using System;
using System.Collections.Generic;

namespace Coop.Steam;

/// <inheritdoc cref="ISteamLobbyApi"/>
/// <remarks>
/// Uses TaleWorlds' initialized user Steam runtime and frame callback pump. Callback bodies catch
/// their own failures because Steam's dispatcher otherwise hides them from the mod log.
/// </remarks>
public class SteamLobbyApi : ISteamPublicLobbyApi
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamLobbyApi>();

    private readonly Callback<GameLobbyJoinRequested_t> lobbyJoinRequestedCallback;
    private readonly Callback<GameRichPresenceJoinRequested_t> richPresenceJoinRequestedCallback;
    private readonly Callback<NewUrlLaunchParameters_t> newLaunchParametersCallback;

    private CallResult<LobbyCreated_t> lobbyCreated;
    private CallResult<LobbyEnter_t> lobbyEntered;
    private CallResult<LobbyMatchList_t> lobbyMatchList;

    private bool createInFlight;
    private bool joinInFlight;
    private bool listInFlight;

    private Action<ulong, bool> onCreateCompleted;
    private Action<ulong, bool> onJoinCompleted;
    private Action<IReadOnlyList<ulong>, bool> onListCompleted;

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
            Logger.Information("Steam lobby join requested for lobby {LobbyId}", callback.m_steamIDLobby.m_SteamID.ToString());
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
            Logger.Information("Steam launch parameters updated");
            ConnectStringReceived?.Invoke(commandLine);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "NewUrlLaunchParameters handler failed");
        }
    }

    public void CreateFriendsOnlyLobby(int maxMembers, Action<ulong, bool> onCompleted)
        => CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers, onCompleted);

    public void CreatePublicLobby(int maxMembers, Action<ulong, bool> onCompleted)
        => CreateLobby(ELobbyType.k_ELobbyTypePublic, maxMembers, onCompleted);

    private void CreateLobby(ELobbyType lobbyType, int maxMembers, Action<ulong, bool> onCompleted)
    {
        onCreateCompleted = onCompleted;
        createInFlight = true;
        lobbyCreated ??= CallResult<LobbyCreated_t>.Create();

        try
        {
            var call = SteamMatchmaking.CreateLobby(lobbyType, maxMembers);
            lobbyCreated.Set(call, OnLobbyCreated);
        }
        catch
        {
            createInFlight = false;
            throw;
        }
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

        try
        {
            var call = SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
            lobbyEntered.Set(call, OnLobbyEntered);
        }
        catch
        {
            joinInFlight = false;
            throw;
        }
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

    public void RequestLobbyList(Action<IReadOnlyList<ulong>, bool> onCompleted)
    {
        if (listInFlight)
        {
            onCompleted(Array.Empty<ulong>(), false);
            return;
        }

        onListCompleted = onCompleted;
        listInFlight = true;
        lobbyMatchList ??= CallResult<LobbyMatchList_t>.Create();

        try
        {
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                LobbyDataCodec.LobbyTypeKey,
                LobbyDataCodec.StandaloneLobbyType,
                ELobbyComparison.k_ELobbyComparisonEqual);

            lobbyMatchList.Set(SteamMatchmaking.RequestLobbyList(), OnLobbyMatchList);
        }
        catch
        {
            listInFlight = false;
            throw;
        }
    }

    private void OnLobbyMatchList(LobbyMatchList_t result, bool ioFailure)
    {
        listInFlight = false;

        try
        {
            if (ioFailure)
            {
                Logger.Error("Steam lobby search failed");
                onListCompleted?.Invoke(Array.Empty<ulong>(), false);
                return;
            }

            var lobbyIds = new List<ulong>((int)result.m_nLobbiesMatching);
            for (uint i = 0; i < result.m_nLobbiesMatching; i++)
            {
                lobbyIds.Add(SteamMatchmaking.GetLobbyByIndex((int)i).m_SteamID);
            }

            onListCompleted?.Invoke(lobbyIds, true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LobbyMatchList handler failed");
            onListCompleted?.Invoke(Array.Empty<ulong>(), false);
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

    public ulong GetLobbyOwner(ulong lobbyId)
    {
        return SteamMatchmaking.GetLobbyOwner(new CSteamID(lobbyId)).m_SteamID;
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
        if (!listInFlight) lobbyMatchList?.Dispose();
    }
}
