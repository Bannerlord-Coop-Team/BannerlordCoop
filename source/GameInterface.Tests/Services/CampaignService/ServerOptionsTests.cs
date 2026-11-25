using GameInterface.Services.CampaignService.Data;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Services.CampaignService;

public class ServerOptionsTests
{
    // The join save is the only way the flag reaches a client, so a dropped field would silently skip the resume popup.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerOptions_RoundTrip_KeepsWorldStartsOffered(bool worldStartsOffered)
    {
        var received = Serializer.DeepClone(new ServerOptions(3, worldStartsOffered));

        Assert.Equal(3, received.PlayerReceivedDamage);
        Assert.Equal(worldStartsOffered, received.WorldStartsOffered);
    }
}
