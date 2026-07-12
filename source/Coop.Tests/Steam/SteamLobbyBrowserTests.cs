using Common.Network.Session;
using Coop.Steam;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Steam;

public class SteamLobbyBrowserTests
{
    private readonly FakeSteamLobbyApi api = new FakeSteamLobbyApi();
    private readonly SteamLobbyBrowser browser;

    public SteamLobbyBrowserTests()
    {
        browser = new SteamLobbyBrowser(api);
    }

    private void AddLobby(ulong lobbyId, SessionJoinInfo info)
    {
        info.ModVersion ??= Common.ModInformation.BuildVersion;
        api.ListedLobbyIds.Add(lobbyId);
        foreach (var pair in LobbyDataCodec.Encode(info))
        {
            api.SetLobbyData(lobbyId, pair.Key, pair.Value);
        }
    }

    [Fact]
    public void RequestLobbies_ReturnsStandaloneMetadata()
    {
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            ServerSteamId = 76561198000000042,
            ModVersion = Common.ModInformation.BuildVersion,
            PasswordRequired = true,
        });

        IReadOnlyList<SteamLobbySummary> results = null;
        string error = null;
        browser.RequestLobbies((lobbies, failure) => (results, error) = (lobbies, failure));

        var lobby = Assert.Single(results);
        Assert.Equal(42UL, lobby.LobbyId);
        Assert.Equal(SessionJoinInfo.CurrentVersion, lobby.ProtocolVersion);
        Assert.Equal(Common.ModInformation.BuildVersion, lobby.ModVersion);
        Assert.True(lobby.PasswordRequired);
        Assert.True(lobby.IsCompatible);
        Assert.Null(error);
    }

    [Fact]
    public void RequestLobbies_FiltersPlayerAndMalformedLobbies()
    {
        AddLobby(41, new SessionJoinInfo { Port = 4200 });
        AddLobby(42, new SessionJoinInfo { Port = 4200, ServerSteamId = 76561198000000042 });
        api.SetLobbyData(42, LobbyDataCodec.ServerSteamIdKey, "not-an-id");

        IReadOnlyList<SteamLobbySummary> results = null;
        browser.RequestLobbies((lobbies, _) => results = lobbies);

        Assert.Empty(results);
    }

    [Fact]
    public void RequestLobbies_PreservesIncompatibleVersionForDisplay()
    {
        AddLobby(42, new SessionJoinInfo
        {
            Version = SessionJoinInfo.CurrentVersion + 1,
            Port = 4200,
            ServerSteamId = 76561198000000042,
        });

        IReadOnlyList<SteamLobbySummary> results = null;
        browser.RequestLobbies((lobbies, _) => results = lobbies);

        Assert.False(Assert.Single(results).IsCompatible);
    }

    [Fact]
    public void RequestLobbies_PreservesDifferentModVersionButMarksItIncompatible()
    {
        const string otherVersion = "different-build";
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            ServerSteamId = 76561198000000042,
            ModVersion = otherVersion,
        });

        IReadOnlyList<SteamLobbySummary> results = null;
        browser.RequestLobbies((lobbies, _) => results = lobbies);

        var lobby = Assert.Single(results);
        Assert.Equal(otherVersion, lobby.ModVersion);
        Assert.False(lobby.IsCompatible);
    }

    [Fact]
    public void RequestLobbies_TreatsTextualPasswordFlagAsFalse()
    {
        AddLobby(42, new SessionJoinInfo
        {
            Port = 4200,
            ServerSteamId = 76561198000000042,
            PasswordRequired = true,
        });
        api.SetLobbyData(42, LobbyDataCodec.PasswordRequiredKey, "true");

        IReadOnlyList<SteamLobbySummary> results = null;
        browser.RequestLobbies((lobbies, _) => results = lobbies);

        Assert.False(Assert.Single(results).PasswordRequired);
    }

    [Fact]
    public void RequestLobbies_ReportsSteamFailure()
    {
        api.ListSucceeds = false;

        string error = null;
        browser.RequestLobbies((_, failure) => error = failure);

        Assert.NotNull(error);
    }

    [Fact]
    public void RequestLobbies_SynchronousFailureDoesNotBlockRetry()
    {
        api.ThrowOnListRequest = true;

        string firstError = null;
        browser.RequestLobbies((_, failure) => firstError = failure);

        api.ThrowOnListRequest = false;
        AddLobby(42, new SessionJoinInfo { Port = 4200, ServerSteamId = 76561198000000042 });
        IReadOnlyList<SteamLobbySummary> retry = null;
        browser.RequestLobbies((lobbies, _) => retry = lobbies);

        Assert.NotNull(firstError);
        Assert.Single(retry);
    }

    [Fact]
    public void RequestLobbies_RejectsOverlappingRefreshWithoutReplacingFirst()
    {
        api.CompleteOperationsImmediately = false;
        AddLobby(42, new SessionJoinInfo { Port = 4200, ServerSteamId = 76561198000000042 });

        IReadOnlyList<SteamLobbySummary> first = null;
        string secondError = null;
        browser.RequestLobbies((lobbies, _) => first = lobbies);
        browser.RequestLobbies((_, failure) => secondError = failure);

        api.CompletePendingList();

        Assert.Single(first);
        Assert.NotNull(secondError);
    }
}
