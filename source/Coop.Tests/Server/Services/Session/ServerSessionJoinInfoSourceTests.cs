using Coop.Core.Common.Configuration;
using Coop.Core.Server.Services.Session;
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
        var source = new ServerSessionJoinInfoSource(new SessionAdvertisementConfig(), networkConfig);

        var info = source.Get();

        Assert.Equal(expected, info.PasswordRequired);
        Assert.Null(info.Password);
    }
}
