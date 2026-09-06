using System;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
using System.Collections.Generic;
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
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

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

    public sealed class SteamHostLobbyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.steam";

        public string Name => "host_lobby";

        public string Description => "Runs the host lobby debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!SessionDiscovery.SteamAvailable) return Failed("Steam integration inactive");
            if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser)) return Failed("No session advertiser; join a session first");
            if (!ContainerProvider.TryResolve<ISessionJoinInfoSource>(out var joinInfoSource)) return Failed("No join info source; join a session first");
            if (!ContainerProvider.TryResolve<INetworkConfig>(out var networkConfig)) return Failed("No network config; join a session first");
            if (!ContainerProvider.TryResolve<ISessionTunnelHost>(out var tunnelHost)) return Failed("No session tunnel host; join a session first");

            // Only the host's own client (connected over loopback) may advertise the session; a
            // tunneled joiner's loopback address is its own join pump, not a local server.
            if (networkConfig.IsTunneled || !TunnelAdvertisement.IsLoopbackAddress(networkConfig.Address))
                return Failed("Run coop.debug.steam.host_lobby on the host's own client (connected to localhost)");

            var info = joinInfoSource.Get();
            TunnelAdvertisement.StartAndStamp(tunnelHost, networkConfig, info);

            advertiser.Advertise(info);
            return Succeeded($"Advertising session (address='{info.Address}', port={info.Port}, version={info.Version})");
        }
    }

    public sealed class SteamInviteCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.steam";

        public string Name => "invite";

        public string Description => "Runs the invite debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser)) return Failed("No session advertiser; join a session first");
            if (!advertiser.IsAdvertising) return Failed("Not advertising; run coop.debug.steam.host_lobby first or enable Steam invites when connecting");

            if (!advertiser.InviteFriends())
                return Failed(SessionInviteText.OverlayUnavailableHint);

            return Succeeded("Invite dialog opened");
        }
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
}
