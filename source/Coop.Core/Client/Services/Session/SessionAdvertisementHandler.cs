using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Network.Session;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Configuration;
using GameInterface.Services.GameDebug.Messages;
using System;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Drives the session advertisement lifecycle on the hosting player's client: start the
/// tunnel and advertise once the connection to the server is up, withdraw on disconnect.
/// Provider calls run on the game thread because the storefront callback pumps dispatch there.
/// </summary>
public class SessionAdvertisementHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ISessionAdvertiser sessionAdvertiser;
    private readonly ISessionTunnelHost sessionTunnelHost;
    private readonly IPeerIdentityPublisher peerIdentityPublisher;
    private readonly ISessionJoinInfoSource joinInfoSource;
    private readonly SessionAdvertisementConfig advertisementConfig;
    private readonly INetworkConfig networkConfig;
    private int connectedPlayers;
    private bool connected;

    public SessionAdvertisementHandler(
        IMessageBroker messageBroker,
        ISessionAdvertiser sessionAdvertiser,
        ISessionTunnelHost sessionTunnelHost,
        IPeerIdentityPublisher peerIdentityPublisher,
        ISessionJoinInfoSource joinInfoSource,
        SessionAdvertisementConfig advertisementConfig,
        INetworkConfig networkConfig)
    {
        this.messageBroker = messageBroker;
        this.sessionAdvertiser = sessionAdvertiser;
        this.sessionTunnelHost = sessionTunnelHost;
        this.peerIdentityPublisher = peerIdentityPublisher;
        this.joinInfoSource = joinInfoSource;
        this.advertisementConfig = advertisementConfig;
        this.networkConfig = networkConfig;

        messageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
        messageBroker.Subscribe<NetworkConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        messageBroker.Subscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkConnected>(Handle_NetworkConnected);
        messageBroker.Unsubscribe<NetworkConnectedPlayersChanged>(Handle_ConnectedPlayersChanged);
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    internal void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
    {
        if (!advertisementConfig.EnablePlatformInvites || !peerIdentityPublisher.IsAvailable) return;

        GameThread.RunSafe(() =>
        {
            connected = true;
            AdvertiseCurrentSession();
        }, context: "AdvertiseSession");
    }

    internal void Handle_ConnectedPlayersChanged(MessagePayload<NetworkConnectedPlayersChanged> payload)
    {
        int count = Math.Max(0, payload.What.ConnectedPlayers);
        GameThread.RunSafe(() =>
        {
            connectedPlayers = count;
            if (connected) AdvertiseCurrentSession();
        }, context: "RefreshHostedSessionPlayers");
    }

    private void AdvertiseCurrentSession()
    {
        if (!advertisementConfig.EnablePlatformInvites || !peerIdentityPublisher.IsAvailable) return;

        var info = joinInfoSource.Get();
        info.ConnectedPlayers = connectedPlayers;

        // The tunnel must listen before the lobby exists, so no joiner can race it.
        TunnelAdvertisement.StartAndStamp(sessionTunnelHost, networkConfig, info);

        sessionAdvertiser.Advertise(info);

        if (!info.HasAddress && !sessionTunnelHost.IsListening)
        {
            messageBroker.Publish(this, new SendInformationMessage(
                "Platform invites are on but no public address or provider tunnel is available; friends cannot connect"));
        }
    }

    internal void Handle_NetworkDisconnected(MessagePayload<NetworkDisconnected> obj)
    {
        GameThread.RunSafe(() =>
        {
            connected = false;
            sessionAdvertiser.StopAdvertising();
            sessionTunnelHost.Stop();
        }, context: "StopAdvertisingSession");
    }
}
