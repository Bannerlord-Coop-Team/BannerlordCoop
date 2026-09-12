using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Configuration;
using GameInterface.Services.GameDebug.Messages;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Drives the session advertisement lifecycle on the hosting player's client: start the
/// tunnel and advertise once the connection to the server is up, withdraw on disconnect.
/// Steam calls run on the game thread because the game's own pump dispatches Steam
/// callbacks there.
/// </summary>
public class SessionAdvertisementHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ISessionAdvertiser sessionAdvertiser;
    private readonly ISessionTunnelHost sessionTunnelHost;
    private readonly ISessionJoinInfoSource joinInfoSource;
    private readonly SessionAdvertisementConfig advertisementConfig;
    private readonly INetworkConfig networkConfig;

    public SessionAdvertisementHandler(
        IMessageBroker messageBroker,
        ISessionAdvertiser sessionAdvertiser,
        ISessionTunnelHost sessionTunnelHost,
        ISessionJoinInfoSource joinInfoSource,
        SessionAdvertisementConfig advertisementConfig,
        INetworkConfig networkConfig)
    {
        this.messageBroker = messageBroker;
        this.sessionAdvertiser = sessionAdvertiser;
        this.sessionTunnelHost = sessionTunnelHost;
        this.joinInfoSource = joinInfoSource;
        this.advertisementConfig = advertisementConfig;
        this.networkConfig = networkConfig;

        messageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
        messageBroker.Subscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkConnected>(Handle_NetworkConnected);
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    internal void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
    {
        if (!advertisementConfig.EnableSteamInvites) return;

        var info = joinInfoSource.Get();

        GameThread.RunSafe(() =>
        {
            // The tunnel must listen before the lobby exists, so no joiner can race it.
            TunnelAdvertisement.StartAndStamp(sessionTunnelHost, networkConfig, info);

            sessionAdvertiser.Advertise(info);

            if (!info.HasAddress && !sessionTunnelHost.IsListening)
            {
                messageBroker.Publish(this, new SendInformationMessage(
                    "Steam invites are on but no public address or Steam relay is available; friends cannot connect"));
            }
        }, context: "AdvertiseSession");
    }

    internal void Handle_NetworkDisconnected(MessagePayload<NetworkDisconnected> obj)
    {
        GameThread.RunSafe(() =>
        {
            sessionAdvertiser.StopAdvertising();
            sessionTunnelHost.Stop();
        }, context: "StopAdvertisingSession");
    }
}
