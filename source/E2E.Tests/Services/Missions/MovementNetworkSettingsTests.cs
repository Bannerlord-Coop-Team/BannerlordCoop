using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;
using Missions.Agents.Handlers;
using Moq;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class MovementNetworkSettingsTests
{
    [Fact]
    public void MissingValuesUseFiveMiBDefaults()
    {
        MovementNetworkSettings settings = Create();

        Assert.Equal(MovementNetworkSettings.BytesPerMiB * 5, settings.OutgoingBytesPerSecond);
        Assert.Equal(MovementNetworkSettings.BytesPerMiB * 5, settings.IncomingBytesPerSecond);
    }

    [Fact]
    public void CoopOptionsValuesConvertMiBToBytes()
    {
        MovementNetworkSettings settings = Create(0.5d, 2d);

        Assert.Equal(MovementNetworkSettings.BytesPerMiB / 2, settings.OutgoingBytesPerSecond);
        Assert.Equal(MovementNetworkSettings.BytesPerMiB * 2, settings.IncomingBytesPerSecond);
    }

    [Fact]
    public void PositiveSubByteValueClampsToOneBytePerSecond()
    {
        MovementNetworkSettings settings = Create(0.0000001d, 0.0000001d);

        Assert.Equal(1, settings.OutgoingBytesPerSecond);
        Assert.Equal(1, settings.IncomingBytesPerSecond);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.MaxValue)]
    [InlineData(2048d)]
    public void InvalidValuesUseDefaults(double invalid)
    {
        MovementNetworkSettings settings = Create(invalid, invalid);

        Assert.Equal(MovementNetworkSettings.BytesPerMiB * 5, settings.OutgoingBytesPerSecond);
        Assert.Equal(MovementNetworkSettings.BytesPerMiB * 5, settings.IncomingBytesPerSecond);
    }

    private static MovementNetworkSettings Create(
        double? uploadMiBPerSecond = null,
        double? downloadMiBPerSecond = null)
    {
        var localBandwidth = new Mock<ILocalMovementBandwidth>();
        localBandwidth.SetupGet(value => value.UploadMiBPerSecond).Returns(uploadMiBPerSecond);
        localBandwidth.SetupGet(value => value.DownloadMiBPerSecond).Returns(downloadMiBPerSecond);
        return new MovementNetworkSettings(localBandwidth.Object);
    }
}
