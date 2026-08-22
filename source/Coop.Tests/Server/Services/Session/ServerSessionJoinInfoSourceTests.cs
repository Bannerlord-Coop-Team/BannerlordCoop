using Common.Network.Session;
using Coop.Core.Common.Configuration;
using Coop.Core.Server.Services.Session;
using Moq;
using Xunit;

namespace Coop.Tests.Server.Services.Session;

public class ServerSessionJoinInfoSourceTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Secret", true)]
    public void Get_AdvertisesOnlyWhetherPasswordIsRequired(string password, bool expected)
    {
        var networkConfig = new NetworkConfig { Token = password };
        var transportTargetSource = new Mock<ISessionTransportTargetSource>();
        transportTargetSource.SetupGet(value => value.PublicAddress).Returns("203.0.113.1");
        transportTargetSource.SetupGet(value => value.TunnelTarget)
            .Returns(new PlatformIdentity("gog", "server"));
        var source = new ServerSessionJoinInfoSource(networkConfig, transportTargetSource.Object);

        var info = source.Get();

        Assert.Equal(Common.ModInformation.BuildVersion, info.ModVersion);
        Assert.Equal(expected, info.PasswordRequired);
        Assert.Null(info.Password);
        Assert.Equal("203.0.113.1", info.Address);
        Assert.Equal(new PlatformIdentity("gog", "server"), info.TunnelTarget);
    }
}
