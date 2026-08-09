using Common.Network;
using Common;
using Common.Network.Session;
using Common.Network.Messages;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Session;
using Coop.Core.Common.Configuration;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Client.Services.Session;

public class SessionAdvertisementHandlerTests
{
    [Fact]
    public void NetworkConnected_WhenSteamInvitesAreDisabled_DoesNotAdvertiseOrStartTunnel()
    {
        var messageBroker = new TestMessageBroker();
        var advertiser = new Mock<ISessionAdvertiser>();
        var tunnelHost = new Mock<ISessionTunnelHost>();
        var joinInfoSource = new Mock<ISessionJoinInfoSource>();

        using var handler = new SessionAdvertisementHandler(
            messageBroker,
            advertiser.Object,
            tunnelHost.Object,
            NoopPeerIdentityPublisher.Instance,
            joinInfoSource.Object,
            new SessionAdvertisementConfig { EnablePlatformInvites = false },
            new NetworkConfig());

        messageBroker.Publish(this, new NetworkConnected());

        joinInfoSource.Verify(source => source.Get(), Times.Never);
        advertiser.Verify(value => value.Advertise(It.IsAny<SessionJoinInfo>()), Times.Never);
        tunnelHost.Verify(value => value.Start(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void ConnectedPlayersChanged_RefreshesPlayerOwnedAdvertisement()
    {
        var messageBroker = new TestMessageBroker();
        var advertiser = new Mock<ISessionAdvertiser>();
        var tunnelHost = new Mock<ISessionTunnelHost>();
        var identityPublisher = new Mock<IPeerIdentityPublisher>();
        var joinInfoSource = new Mock<ISessionJoinInfoSource>();
        var advertised = new List<SessionJoinInfo>();
        identityPublisher.SetupGet(value => value.IsAvailable).Returns(true);
        tunnelHost.SetupGet(value => value.IsListening).Returns(true);
        joinInfoSource.Setup(value => value.Get()).Returns(() => new SessionJoinInfo());
        advertiser
            .Setup(value => value.Advertise(It.IsAny<SessionJoinInfo>()))
            .Callback<SessionJoinInfo>(advertised.Add);

        using var handler = new SessionAdvertisementHandler(
            messageBroker,
            advertiser.Object,
            tunnelHost.Object,
            identityPublisher.Object,
            joinInfoSource.Object,
            new SessionAdvertisementConfig { EnablePlatformInvites = true },
            new NetworkConfig { Address = "127.0.0.1" });

        messageBroker.Publish(this, new NetworkConnected());
        messageBroker.Publish(this, new NetworkConnectedPlayersChanged(3));
        DrainGameThread();

        Assert.Equal(2, advertised.Count);
        Assert.Equal(0, advertised[0].ConnectedPlayers);
        Assert.Equal(3, advertised[1].ConnectedPlayers);
    }

    [Fact]
    public void NetworkConnected_WithoutIdentityBridge_DoesNotAdvertiseTunnel()
    {
        var messageBroker = new TestMessageBroker();
        var advertiser = new Mock<ISessionAdvertiser>();
        var tunnelHost = new Mock<ISessionTunnelHost>();
        var joinInfoSource = new Mock<ISessionJoinInfoSource>();

        using var handler = new SessionAdvertisementHandler(
            messageBroker,
            advertiser.Object,
            tunnelHost.Object,
            NoopPeerIdentityPublisher.Instance,
            joinInfoSource.Object,
            new SessionAdvertisementConfig { EnablePlatformInvites = true },
            new NetworkConfig { Address = "127.0.0.1" });

        messageBroker.Publish(this, new NetworkConnected());
        DrainGameThread();

        joinInfoSource.Verify(value => value.Get(), Times.Never);
        advertiser.Verify(value => value.Advertise(It.IsAny<SessionJoinInfo>()), Times.Never);
        tunnelHost.Verify(value => value.Start(It.IsAny<int>()), Times.Never);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}
