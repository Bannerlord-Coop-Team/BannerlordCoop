using GameInterface.Configuration;
using Missions.Agents.Handlers;
using Moq;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class MovementNetworkSettingsTests
{
    [Fact]
    public void MissingValuesUseOneMiBDefaults()
    {
        MovementNetworkSettings settings = Create(new NetworkConfigData());

        Assert.Equal(MovementNetworkSettings.BytesPerMiB, settings.OutgoingBytesPerSecond);
        Assert.Equal(MovementNetworkSettings.BytesPerMiB, settings.IncomingBytesPerSecond);
    }

    [Fact]
    public void ConfiguredValuesConvertMiBToBytes()
    {
        MovementNetworkSettings settings = Create(new NetworkConfigData
        {
            MovementOutgoingMiBPerSecond = 0.5d,
            MovementIncomingMiBPerSecond = 2d,
        });

        Assert.Equal(MovementNetworkSettings.BytesPerMiB / 2, settings.OutgoingBytesPerSecond);
        Assert.Equal(MovementNetworkSettings.BytesPerMiB * 2, settings.IncomingBytesPerSecond);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.MaxValue)]
    [InlineData(2048d)]
    public void InvalidValuesUseDefaults(double invalid)
    {
        MovementNetworkSettings settings = Create(new NetworkConfigData
        {
            MovementOutgoingMiBPerSecond = invalid,
            MovementIncomingMiBPerSecond = invalid,
        });

        Assert.Equal(MovementNetworkSettings.BytesPerMiB, settings.OutgoingBytesPerSecond);
        Assert.Equal(MovementNetworkSettings.BytesPerMiB, settings.IncomingBytesPerSecond);
    }

    private static MovementNetworkSettings Create(NetworkConfigData network)
    {
        var modConfig = new Mock<IModConfig>();
        modConfig.SetupGet(value => value.Data).Returns(new ModConfigData
        {
            Network = network,
        });
        return new MovementNetworkSettings(modConfig.Object);
    }
}
