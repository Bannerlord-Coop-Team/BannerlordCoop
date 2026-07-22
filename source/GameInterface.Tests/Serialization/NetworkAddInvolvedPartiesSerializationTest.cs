using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Surrogates;
using ProtoBuf.Meta;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization;

/// <summary>
/// Verifies that <see cref="NetworkAddInvolvedParties"/> survives a protobuf round-trip
/// with its party ids and per-party positions kept index-aligned, so clients place each
/// involved party at the position the server captured for it.
/// </summary>
public class NetworkAddInvolvedPartiesSerializationTest
{
    public NetworkAddInvolvedPartiesSerializationTest()
    {
        // Registers all surrogates (including CampaignVec2) with RuntimeTypeModel.
        // The lock inside SurrogateCollection makes repeated calls safe across tests.
        new SurrogateCollection();
    }

    [Fact]
    public void RoundTrip_PreservesPartyIdsAndPositions()
    {
        var original = new NetworkAddInvolvedParties(
            "mapEvent_1",
            new[] { "party_1", "party_2" },
            new[]
            {
                new CampaignVec2(new Vec2(12.5f, -3.25f), true),
                new CampaignVec2(new Vec2(40f, 80.75f), false),
            },
            new[] { 125, 75 },
            new[] { false, true },
            new[] { true, false });

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        NetworkAddInvolvedParties result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkAddInvolvedParties)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkAddInvolvedParties));
        }

        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(original.MapEventPartyIds, result.MapEventPartyIds);
        Assert.Equal(original.InitialSpawnCounts, result.InitialSpawnCounts);
        Assert.Equal(original.PostPlanAdditions, result.PostPlanAdditions);
        Assert.Equal(original.WaitsForPrioritySpawn, result.WaitsForPrioritySpawn);

        // Positions must stay index-aligned with the party ids so each party snaps to the
        // right place on the client.
        Assert.Equal(original.Positions.Length, result.Positions.Length);
        for (int i = 0; i < original.Positions.Length; i++)
        {
            Assert.Equal(original.Positions[i].X, result.Positions[i].X);
            Assert.Equal(original.Positions[i].Y, result.Positions[i].Y);
            Assert.Equal(original.Positions[i].IsOnLand, result.Positions[i].IsOnLand);
        }
    }

    [Fact]
    public void PrioritySlotRoundTrip_PreservesAssignment()
    {
        var original = new NetworkBattlePrioritySlotAssigned(
            "mapEvent_1",
            1949,
            "waiting_party",
            "donor_party");

        NetworkBattlePrioritySlotAssigned result;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            ms.Position = 0;
            result = (NetworkBattlePrioritySlotAssigned)RuntimeTypeModel.Default.Deserialize(
                ms,
                null,
                typeof(NetworkBattlePrioritySlotAssigned));
        }

        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(original.TransferId, result.TransferId);
        Assert.Equal(original.WaitingPartyId, result.WaitingPartyId);
        Assert.Equal(original.DonorPartyId, result.DonorPartyId);
    }

    [Fact]
    public void UnassignedPriorityCancellationRoundTrip_PreservesWaitingParty()
    {
        var original = new NetworkBattlePrioritySlotCancelled(
            "mapEvent_1",
            0,
            "waiting_party",
            null);

        NetworkBattlePrioritySlotCancelled result;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            ms.Position = 0;
            result = (NetworkBattlePrioritySlotCancelled)RuntimeTypeModel.Default.Deserialize(
                ms,
                null,
                typeof(NetworkBattlePrioritySlotCancelled));
        }

        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(0, result.TransferId);
        Assert.Equal(original.WaitingPartyId, result.WaitingPartyId);
        Assert.Null(result.DonorPartyId);
    }
}
