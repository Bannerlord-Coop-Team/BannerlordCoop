using Coop.Core.Common.Session;
using Xunit;

namespace Coop.Tests.Session;

public class ManagedServerConfigTests
{
    [Fact]
    public void Port_DefaultsTo4200AndRejectsOutOfRangeValues()
    {
        var previous = ManagedServerConfig.Port;
        try
        {
            ManagedServerConfig.Port = ManagedServerConfig.DefaultPort;

            Assert.Equal(4200, ManagedServerConfig.Port);
            Assert.True(ManagedServerConfig.IsValidPort(1));
            Assert.True(ManagedServerConfig.IsValidPort(65535));
            Assert.False(ManagedServerConfig.IsValidPort(0));
            Assert.False(ManagedServerConfig.IsValidPort(65536));
        }
        finally
        {
            ManagedServerConfig.Port = previous;
        }
    }
}
