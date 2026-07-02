using Common;
using Common.Messaging;
using Common.Network.Session;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Configuration;
using GameInterface.Services.GameDebug.Messages;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Drives the session advertisement lifecycle on the hosting player's client: advertise
/// once the connection to the server is up, withdraw on disconnect. Steam calls run on the
/// game thread because the game's own pump dispatches Steam callbacks there.
/// </summary>
public class SessionAdvertisementHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ISessionAdvertiser sessionAdvertiser;
    private readonly ISessionJoinInfoSource joinInfoSource;
    private readonly SessionAdvertisementConfig advertisementConfig;

    public SessionAdvertisementHandler(
        IMessageBroker messageBroker,
        ISessionAdvertiser sessionAdvertiser,
        ISessionJoinInfoSource joinInfoSource,
        SessionAdvertisementConfig advertisementConfig)
    {
        this.messageBroker = messageBroker;
        this.sessionAdvertiser = sessionAdvertiser;
        this.joinInfoSource = joinInfoSource;
        this.advertisementConfig = advertisementConfig;

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
            sessionAdvertiser.Advertise(info);

            if (!info.HasAddress)
            {
                messageBroker.Publish(this, new SendInformationMessage(
                    "Steam invites are on but no public address is set; friends cannot connect until you set one on the co-op screen"));
            }
        }, context: "AdvertiseSession");
    }

    internal void Handle_NetworkDisconnected(MessagePayload<NetworkDisconnected> obj)
    {
        GameThread.RunSafe(sessionAdvertiser.StopAdvertising, context: "StopAdvertisingSession");
    }
}
