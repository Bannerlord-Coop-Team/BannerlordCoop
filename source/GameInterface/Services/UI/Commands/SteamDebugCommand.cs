using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
using System;
using System.Collections.Generic;
#if DEBUG
using System.Linq;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
#endif
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.UI.Commands;

/// <summary>
/// Debug commands for driving the Steam session discovery flow. Run these in the client
/// process that owns the session (the hosting player's client); the dedicated server has
/// no Steam services. Example: host with Steam invites enabled, then
/// <c>coop.debug.steam.invite</c> on the host's client, or
/// <c>coop.debug.steam.join 109775244567890123</c> on a friend's client at the main menu.
/// </summary>
public class SteamDebugCommand
{
#if DEBUG
    private static readonly SteamLobbySummary[] MockLobbies =
    {
        CreateMockLobby(1, "Pagination Alpha 01", 1),
        CreateMockLobby(2, "Other Bravo 02", 2),
        CreateMockLobby(3, "Pagination Alpha 03", 3),
        CreateMockLobby(4, "Other Delta 04", 4),
        CreateMockLobby(5, "Pagination Alpha 05", 5),
        CreateMockLobby(6, "Other Foxtrot 06", 6),
        CreateMockLobby(7, "Pagination Alpha 07", 7),
        CreateMockLobby(8, "Pagination Alpha 08", 8),
        CreateMockLobby(9, "Pagination Alpha 09", 9),
    };
#endif

    [CommandLineArgumentFunction("status", "coop.debug.steam")]
    public static string Status(List<string> args)
    {
        if (!SessionDiscovery.SteamAvailable) return "Steam integration inactive (Steam not running or not a Steam install)";
        if (!ContainerProvider.TryGetContainer(out _)) return "Steam integration active; no co-op session running";
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser)) return "Steam integration active; this process has no session advertiser (server process?)";

        if (ContainerProvider.TryResolve<ISessionTunnelHost>(out var tunnelHost))
        {
            return $"Steam integration active; advertising={advertiser.IsAdvertising}; " +
                $"tunnelListening={tunnelHost.IsListening}; tunnelPeers={tunnelHost.PeerCount}";
        }

        return $"Steam integration active; advertising={advertiser.IsAdvertising}";
    }

    [CommandLineArgumentFunction("host_lobby", "coop.debug.steam")]
    public static string HostLobby(List<string> args)
    {
        if (!SessionDiscovery.SteamAvailable) return "Steam integration inactive";
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser)) return "No session advertiser; join a session first";
        if (!ContainerProvider.TryResolve<ISessionJoinInfoSource>(out var joinInfoSource)) return "No join info source; join a session first";
        if (!ContainerProvider.TryResolve<INetworkConfig>(out var networkConfig)) return "No network config; join a session first";
        if (!ContainerProvider.TryResolve<ISessionTunnelHost>(out var tunnelHost)) return "No session tunnel host; join a session first";

        // Only the host's own client (connected over loopback) may advertise the session; a
        // tunneled joiner's loopback address is its own join pump, not a local server.
        if (networkConfig.IsTunneled || !TunnelAdvertisement.IsLoopbackAddress(networkConfig.Address))
            return "Run coop.debug.steam.host_lobby on the host's own client (connected to localhost)";

        var info = joinInfoSource.Get();
        TunnelAdvertisement.StartAndStamp(tunnelHost, networkConfig, info);

        advertiser.Advertise(info);
        return $"Advertising session (address='{info.Address}', port={info.Port}, version={info.Version})";
    }

    [CommandLineArgumentFunction("invite", "coop.debug.steam")]
    public static string Invite(List<string> args)
    {
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser)) return "No session advertiser; join a session first";
        if (!advertiser.IsAdvertising) return "Not advertising; run coop.debug.steam.host_lobby first or enable Steam invites when connecting";

        return advertiser.InviteFriends()
            ? "Invite dialog opened"
            : SessionInviteText.OverlayUnavailableHint;
    }

    [CommandLineArgumentFunction("join", "coop.debug.steam")]
    public static string Join(List<string> args)
    {
        if (!SessionDiscovery.SteamAvailable) return "Steam integration inactive";
        if (args.Count != 1 || !ulong.TryParse(args[0], out var lobbyId) || lobbyId == 0)
            return "Usage: coop.debug.steam.join <lobbyId>";

        MessageBroker.Instance.Publish(null, new JoinSteamLobby(lobbyId));
        return $"Joining Steam lobby {lobbyId}";
    }

#if DEBUG
    [CommandLineArgumentFunction("mock_lobbies", "coop.debug.steam")]
    public static string MockLobbiesCommand(List<string> args)
    {
        if (args.Count == 0) return "Usage: coop.debug.steam.mock_lobbies <open|select|previous|next|search|status|close> [hostName]";

        switch (args[0].ToLowerInvariant())
        {
            case "open":
                return OpenMockLobbies();
            case "status":
                return GetMockLobbyStatus();
            case "select":
            case "previous":
            case "next":
            case "close":
                return DriveMockLobbies(args[0].ToLowerInvariant());
            case "search":
                if (args.Count != 2) return "Usage: coop.debug.steam.mock_lobbies search <hostName>";
                if (!TryGetMockViewModel(out var searchViewModel, out var searchError)) return searchError;
                searchViewModel.SteamLobbyHostSearchText = args[1];
                searchViewModel.ActionSearchSteamLobbies();
                return GetMockLobbyStatus();
            default:
                return $"Unknown mock lobby action '{args[0]}'";
        }
    }

    private static string OpenMockLobbies()
    {
        if (CoopConnectionUI.DebugSteamLobbyBrowser != null || CoopConnectionUI.DebugDataSource != null)
            return "A connection screen is already open";

        CoopConnectionUI.DebugSteamLobbyBrowser = new FixedSteamLobbyBrowser(MockLobbies);
        try
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopConnectionUI>());
        }
        catch
        {
            CoopConnectionUI.DebugSteamLobbyBrowser = null;
            throw;
        }

        return "Opening the Steam lobby screen with 9 mock lobbies";
    }

    private static string DriveMockLobbies(string action)
    {
        if (!TryGetMockViewModel(out var viewModel, out var error)) return error;

        switch (action)
        {
            case "select":
                viewModel.Tabs[1].ExecuteSelection();
                break;
            case "previous":
                viewModel.ActionPreviousSteamLobbyPage();
                break;
            case "next":
                viewModel.ActionNextSteamLobbyPage();
                break;
            case "close":
                ScreenManager.PopScreen();
                return "Closing the mock Steam lobby screen";
        }

        return GetMockLobbyStatus();
    }

    private static string GetMockLobbyStatus()
    {
        var viewModel = CoopConnectionUI.DebugDataSource;
        if (viewModel == null)
            return $"ready=false injected={(CoopConnectionUI.DebugSteamLobbyBrowser != null).ToString().ToLowerInvariant()}";

        string hosts = string.Join(",", viewModel.SteamLobbies.Select(lobby => lobby.HostText));
        return $"ready=true tab={viewModel.SelectedTab?.Id ?? "none"} " +
            $"header=\"{viewModel.SteamLobbiesHeaderText}\" page=\"{viewModel.SteamLobbyPageText}\" " +
            $"pagination={viewModel.IsSteamLobbyPaginationVisible.ToString().ToLowerInvariant()} " +
            $"previousDisabled={viewModel.IsPreviousSteamLobbyPageDisabled.ToString().ToLowerInvariant()} " +
            $"nextDisabled={viewModel.IsNextSteamLobbyPageDisabled.ToString().ToLowerInvariant()} hosts=\"{hosts}\"";
    }

    private static bool TryGetMockViewModel(out CoopConnectMenuVM viewModel, out string error)
    {
        viewModel = CoopConnectionUI.DebugDataSource;
        error = viewModel == null ? "The mock Steam lobby screen is not ready" : null;
        return viewModel != null;
    }

    private static SteamLobbySummary CreateMockLobby(ulong lobbyId, string ownerName, int connectedPlayers)
    {
        return new SteamLobbySummary
        {
            LobbyId = lobbyId,
            OwnerName = ownerName,
            ConnectedPlayers = connectedPlayers,
            ProtocolVersion = SessionJoinInfo.CurrentVersion,
            ModVersion = ModInformation.BuildVersion,
        };
    }

    private sealed class FixedSteamLobbyBrowser : ISteamLobbyBrowser
    {
        private readonly IReadOnlyList<SteamLobbySummary> lobbies;

        public FixedSteamLobbyBrowser(IReadOnlyList<SteamLobbySummary> lobbies)
        {
            this.lobbies = lobbies;
        }

        public void RequestLobbies(Action<IReadOnlyList<SteamLobbySummary>, string> onCompleted)
        {
            onCompleted(lobbies, null);
        }
    }
#endif
}
