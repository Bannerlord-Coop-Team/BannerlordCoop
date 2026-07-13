using Common.Network;
using Common.Network.Session;
using Common.Tests.Utils;
using Coop.Core.Common.Configuration;
using Coop.Core.Server.Services.Session;
using Coop.Core.Server.Services.Session.Messages;
using Moq;
using Xunit;

namespace Coop.Tests.Server.Services.Session;

public class ServerSessionAdvertisementHandlerTests
{
    [Fact]
    public void None_DoesNotStartSteamAdvertisementOrTunnel()
    {
        var messageBroker = new TestMessageBroker();
        var advertiser = new Mock<ISessionAdvertiser>();
        var tunnelHost = new Mock<ISessionTunnelHost>();
        var joinInfoSource = new Mock<ISessionJoinInfoSource>();
        var handler = new ServerSessionAdvertisementHandler(
            messageBroker,
            advertiser.Object,
            tunnelHost.Object,
            joinInfoSource.Object,
            new NetworkConfig(),
            new SessionAdvertisementConfig { Visibility = ServerVisibility.None });

        messageBroker.Publish(this, new ServerListening());
        handler.Dispose();

        joinInfoSource.Verify(source => source.Get(), Times.Never);
        advertiser.Verify(value => value.Advertise(It.IsAny<SessionJoinInfo>()), Times.Never);
        advertiser.Verify(value => value.StopAdvertising(), Times.Never);
        tunnelHost.Verify(value => value.Start(It.IsAny<int>()), Times.Never);
        tunnelHost.Verify(value => value.Stop(), Times.Never);
    }
}
