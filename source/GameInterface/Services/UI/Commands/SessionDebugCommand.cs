using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.UI.Commands;

/// <summary>Debug commands for the active storefront's session discovery flow.</summary>
public class SessionDebugCommand
{
    [CommandLineArgumentFunction("status", "coop.debug.session")]
    public static string Status(List<string> args)
    {
        ISessionProvider provider = ActiveProvider;
        if (provider == null) return "Platform session integration inactive";
        if (!ContainerProvider.TryGetContainer(out _))
            return $"{provider.DisplayName} integration active; no co-op session running";
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser))
            return $"{provider.DisplayName} integration active; this process has no session advertiser";

        if (ContainerProvider.TryResolve<ISessionTunnelHost>(out var tunnelHost))
        {
            return $"{provider.DisplayName} integration active; advertising={advertiser.IsAdvertising}; " +
                $"tunnelListening={tunnelHost.IsListening}; tunnelPeers={tunnelHost.PeerCount}";
        }

        return $"{provider.DisplayName} integration active; advertising={advertiser.IsAdvertising}";
    }

    [CommandLineArgumentFunction("host_listing", "coop.debug.session")]
    public static string HostListing(List<string> args)
    {
        if (ActiveProvider == null) return "Platform session integration inactive";
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser))
            return "No session advertiser; join a session first";
        if (!ContainerProvider.TryResolve<ISessionJoinInfoSource>(out var joinInfoSource))
            return "No join info source; join a session first";
        if (!ContainerProvider.TryResolve<INetworkConfig>(out var networkConfig))
            return "No network config; join a session first";
        if (!ContainerProvider.TryResolve<ISessionTunnelHost>(out var tunnelHost))
            return "No session tunnel host; join a session first";

        // A tunneled joiner's loopback address reaches its own join pump, not the local server.
        if (networkConfig.IsTunneled || !TunnelAdvertisement.IsLoopbackAddress(networkConfig.Address))
            return "Run coop.debug.session.host_listing on the hosting client connected to localhost";

        var info = joinInfoSource.Get();
        TunnelAdvertisement.StartAndStamp(tunnelHost, networkConfig, info);
        advertiser.Advertise(info);
        return $"Advertising session (address='{info.Address}', port={info.Port}, version={info.Version})";
    }

    [CommandLineArgumentFunction("invite", "coop.debug.session")]
    public static string Invite(List<string> args)
    {
        if (!ContainerProvider.TryResolve<ISessionAdvertiser>(out var advertiser))
            return "No session advertiser; join a session first";
        if (!advertiser.IsAdvertising)
            return "Not advertising; run coop.debug.session.host_listing or enable platform invites when connecting";

        return advertiser.InviteFriends()
            ? "Invite dialog opened"
            : SessionInviteText.OverlayUnavailableHint;
    }

    [CommandLineArgumentFunction("join", "coop.debug.session")]
    public static string Join(List<string> args)
    {
        ISessionProvider provider = SessionDiscovery.ClientProvider;
        if (provider == null) return "Platform session integration inactive";
        if (args.Count != 1)
            return "Usage: coop.debug.session.join listing-id";

        var listingId = new SessionListingId(provider.Provider, args[0]);
        if (!listingId.IsValid) return "Enter a valid listing id";

        MessageBroker.Instance.Publish(null, new JoinSessionListing(listingId));
        return $"Joining {provider.DisplayName} listing {listingId.Value}";
    }

    private static ISessionProvider ActiveProvider =>
        SessionDiscovery.ClientProvider ?? SessionDiscovery.ServerProvider;
}
