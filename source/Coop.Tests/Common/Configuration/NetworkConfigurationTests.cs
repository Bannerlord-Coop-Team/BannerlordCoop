using Coop.Core.Common.Configuration;
using Xunit;

namespace Coop.Tests.Common.Configuration;

public class NetworkConfigurationTests
{
    [Fact]
    public void PacketThresholds_AreTiered()
    {
        var config = new NetworkConfiguration();

        // The pause threshold, raised from the original 1000.
        Assert.Equal(10000, config.MaxPacketsInQueue);
        // The slow-down tier sits below the pause tier so the game caps to 1x before it fully pauses.
        Assert.Equal(5000, config.SlowDownPacketThreshold);
        Assert.True(config.SlowDownPacketThreshold < config.MaxPacketsInQueue);
    }
}
