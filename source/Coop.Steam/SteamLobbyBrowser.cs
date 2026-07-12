using Common.Network.Session;
using System;
using System.Collections.Generic;

namespace Coop.Steam;

/// <summary>
/// Converts Steam's public lobby search results into display-safe standalone-server summaries.
/// </summary>
public class SteamLobbyBrowser : ISteamLobbyBrowser
{
    private readonly ISteamPublicLobbyApi lobbyApi;
    private bool requestInFlight;

    public SteamLobbyBrowser(ISteamPublicLobbyApi lobbyApi)
    {
        this.lobbyApi = lobbyApi;
    }

    public void RequestLobbies(Action<IReadOnlyList<SteamLobbySummary>, string> onCompleted)
    {
        if (requestInFlight)
        {
            onCompleted(Array.Empty<SteamLobbySummary>(), "A Steam lobby search is already in progress");
            return;
        }

        requestInFlight = true;
        try
        {
            lobbyApi.RequestLobbyList((lobbyIds, success) => CompleteRequest(lobbyIds, success, onCompleted));
        }
        catch (Exception)
        {
            requestInFlight = false;
            onCompleted(Array.Empty<SteamLobbySummary>(), "Could not retrieve Steam lobbies");
        }
    }

    private void CompleteRequest(IReadOnlyList<ulong> lobbyIds, bool success,
        Action<IReadOnlyList<SteamLobbySummary>, string> onCompleted)
    {
        requestInFlight = false;

        if (!success)
        {
            onCompleted(Array.Empty<SteamLobbySummary>(), "Could not retrieve Steam lobbies");
            return;
        }

        var summaries = new List<SteamLobbySummary>();
        foreach (var lobbyId in lobbyIds)
        {
            if (!string.Equals(
                lobbyApi.GetLobbyData(lobbyId, LobbyDataCodec.LobbyTypeKey),
                LobbyDataCodec.StandaloneLobbyType,
                StringComparison.Ordinal))
            {
                continue;
            }

            if (!ulong.TryParse(lobbyApi.GetLobbyData(lobbyId, LobbyDataCodec.ServerSteamIdKey),
                out var serverSteamId) || serverSteamId == 0)
            {
                continue;
            }

            int.TryParse(lobbyApi.GetLobbyData(lobbyId, LobbyDataCodec.VersionKey), out var protocolVersion);
            var passwordRequired = lobbyApi.GetLobbyData(
                lobbyId, LobbyDataCodec.PasswordRequiredKey) == "1";

            summaries.Add(new SteamLobbySummary
            {
                LobbyId = lobbyId,
                ProtocolVersion = protocolVersion,
                ModVersion = lobbyApi.GetLobbyData(lobbyId, LobbyDataCodec.ModVersionKey),
                PasswordRequired = passwordRequired,
            });
        }

        onCompleted(summaries, null);
    }
}
