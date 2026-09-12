using Common.Network;
using Common.Network.Session;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Session;
using Coop.Core.Common.Configuration;
using Moq;
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
            joinInfoSource.Object,
            new SessionAdvertisementConfig { EnableSteamInvites = false },
            new NetworkConfig());

        messageBroker.Publish(this, new NetworkConnected());

        joinInfoSource.Verify(source => source.Get(), Times.Never);
        advertiser.Verify(value => value.Advertise(It.IsAny<SessionJoinInfo>()), Times.Never);
        tunnelHost.Verify(value => value.Start(It.IsAny<int>()), Times.Never);
    }
}
