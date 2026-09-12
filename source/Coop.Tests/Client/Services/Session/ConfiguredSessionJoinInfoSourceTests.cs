using Coop.Core.Client.Services.Session;
using Coop.Core.Common.Configuration;
using Xunit;

namespace Coop.Tests.Client.Services.Session;

public class ConfiguredSessionJoinInfoSourceTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Secret", true)]
    public void Get_AdvertisesOnlyWhetherPasswordIsRequired(string password, bool expected)
    {
        var source = new ConfiguredSessionJoinInfoSource(new NetworkConfig { Token = password });

        var info = source.Get();

        Assert.Equal(Common.ModInformation.BuildVersion, info.ModVersion);
        Assert.Equal(expected, info.PasswordRequired);
        Assert.Null(info.Password);
    }
}
