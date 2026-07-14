using GameInterface.Services.UI.Notifications.Messages;
using ProtoBuf;
using System.IO;
using TaleWorlds.CampaignSystem.Actions;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkNotifyKingdomInfluenceChangedSerializationTest
{
    [Fact]
    public void RoundTrip_PreservesFields()
    {
        var original = new NetworkNotifyKingdomInfluenceChanged(
            "hero-player",
            "party-player",
            "clan-player",
            42,
            GainKingdomInfluenceAction.InfluenceGainingReason.DonatePrisoners);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);

        Assert.NotEmpty(stream.ToArray());

        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkNotifyKingdomInfluenceChanged>(stream);

        Assert.Equal(original.HeroId, copy.HeroId);
        Assert.Equal(original.MobilePartyId, copy.MobilePartyId);
        Assert.Equal(original.ClanId, copy.ClanId);
        Assert.Equal(original.GainedInfluence, copy.GainedInfluence);
        Assert.Equal(original.Detail, copy.Detail);
    }
}
