using Autofac;
using Common.Messaging;
using Common.Serialization;
using Coop.Core.Server.Services.MobileParties.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.MobileParties;

public class NetworkJoinCampaignBaselineTests
{
    private readonly ServerTestComponent serverComponent;

    public NetworkJoinCampaignBaselineTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
    }

    [Fact]
    public void CampaignBaseline_RoundTripsTimeAndPartyPositions()
    {
        var serializer = serverComponent.Container.Resolve<ICommonSerializer>();
        var message = new NetworkJoinCampaignBaseline(987654321L, new[]
        {
            new MobilePartyPositionData(
                "main_party",
                new CampaignVec2(new Vec2(12.5f, -3.25f), true)),
            new MobilePartyPositionData(
                "looters_1",
                new CampaignVec2(new Vec2(40f, 80.75f), false)),
        });

        byte[] bytes = serializer.Serialize(message);
        var received = Assert.IsType<NetworkJoinCampaignBaseline>(serializer.Deserialize<IMessage>(bytes));

        Assert.Equal(987654321L, received.ServerTicks);
        Assert.Equal(2, received.Positions.Length);
        Assert.Equal("main_party", received.Positions[0].MobilePartyId);
        Assert.Equal(12.5f, received.Positions[0].X);
        Assert.Equal(-3.25f, received.Positions[0].Y);
        Assert.True(received.Positions[0].IsOnLand);
        Assert.Equal("looters_1", received.Positions[1].MobilePartyId);
        Assert.Equal(40f, received.Positions[1].X);
        Assert.Equal(80.75f, received.Positions[1].Y);
        Assert.False(received.Positions[1].IsOnLand);
    }
}
