using Coop.Core.Common.Configuration;
using Xunit;

namespace Coop.Tests.Common.Configuration;

public class NetworkConfigurationTests
{
    [Fact]
    public void MaxPacketsInQueue_IsRaisedTo10000()
    {
        var config = new NetworkConfiguration();

        Assert.Equal(10000, config.MaxPacketsInQueue);
    }
}
