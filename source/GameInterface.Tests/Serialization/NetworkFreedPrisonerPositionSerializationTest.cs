using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Surrogates;
using ProtoBuf.Meta;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization;

/// <summary>
/// Verifies that <see cref="NetworkFreedPrisonerPosition"/> survives a protobuf round-trip with its
/// party id and position intact, so a freed party snaps to the server's authoritative release
/// position on other clients (MobileParty.Position is not auto-synced).
/// </summary>
public class NetworkFreedPrisonerPositionSerializationTest
{
    public NetworkFreedPrisonerPositionSerializationTest()
    {
        // Registers all surrogates (including CampaignVec2) with RuntimeTypeModel.
        // The lock inside SurrogateCollection makes repeated calls safe across tests.
        new SurrogateCollection();
    }

    [Fact]
    public void RoundTrip_PreservesPartyIdAndPosition()
    {
        var original = new NetworkFreedPrisonerPosition(
            "party_1",
            new CampaignVec2(new Vec2(12.5f, -3.25f), true),
            isCurrentlyAtSea: true);

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        NetworkFreedPrisonerPosition result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkFreedPrisonerPosition)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkFreedPrisonerPosition));
        }

        Assert.Equal(original.PartyId, result.PartyId);
        Assert.Equal(original.Position.X, result.Position.X);
        Assert.Equal(original.Position.Y, result.Position.Y);
        Assert.Equal(original.Position.IsOnLand, result.Position.IsOnLand);
        Assert.Equal(original.IsCurrentlyAtSea, result.IsCurrentlyAtSea);
    }
}
