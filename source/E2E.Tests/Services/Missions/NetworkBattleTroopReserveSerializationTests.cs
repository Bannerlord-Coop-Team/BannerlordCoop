using Common.Serialization;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using ProtoBuf.Meta;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Missions;

/// <summary>Wire round-trips for the BR-033 reserve flush handshake messages; needs no game instances.</summary>
public class NetworkBattleTroopReserveSerializationTests
{
    /// <summary>
    /// The reserve allocation and flush fields survive a real protobuf round trip.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void FlushHandshakeFields_RoundTripOverTheWire()
    {
        var reserve = RoundTrip(new NetworkBattleTroopReserve(
            "rt_battle", (int)BattleSideEnum.Attacker,
            new[] { new PartyReserve("own-party", 0,
                new[] { new TroopReserveEntry(42, "priority-character", 0, supplyOrder: 9) },
                isReceiverPlayerParty: true, sideOffset: 7, playerOwnedRank: 2,
                playerOwnedPartiesBefore: 2) },
            sideTotalTroops: 42,
            playerOwnedPartyCount: 3,
            allocationRevision: 17,
            battleSize: 1000,
            flushRequested: true));
        Assert.True(reserve.FlushRequested);
        Assert.Equal(42, reserve.SideTotalTroops);
        Assert.Equal(3, reserve.PlayerOwnedPartyCount);
        Assert.Equal(17, reserve.AllocationRevision);
        Assert.Equal(1000, reserve.BattleSize);
        var party = Assert.Single(reserve.Parties);
        Assert.True(party.IsReceiverPlayerParty);
        Assert.Equal(7, party.SideOffset);
        Assert.Equal(2, party.PlayerOwnedRank);
        Assert.Equal(2, party.PlayerOwnedPartiesBefore);
        Assert.True(party.HasPlayerOwnedPartiesBefore);
        Assert.Equal(9, Assert.Single(party.Entries).SupplyOrder);

        var emptyReserve = RoundTrip(new NetworkBattleTroopReserve(
            "rt_battle", (int)BattleSideEnum.Attacker, Array.Empty<PartyReserve>(),
            sideTotalTroops: 0, playerOwnedPartyCount: 0, allocationRevision: 18, battleSize: 1000));
        Assert.False(emptyReserve.FlushRequested);
        Assert.Equal(0, emptyReserve.SideTotalTroops);
        Assert.Equal(0, emptyReserve.PlayerOwnedPartyCount);
        Assert.Equal(1000, emptyReserve.BattleSize);
        Assert.Equal(18, emptyReserve.AllocationRevision);
        Assert.Empty(emptyReserve.Parties);

        var flush = RoundTrip(new NetworkBattleSupplyProgress(
            "rt_battle", new[] { new SupplyProgressEntry("party", 2) }, isFlush: true));
        Assert.True(flush.IsFlush);
        Assert.Equal(2, flush.Entries.Single().SuppliedCount);

        var legacyReport = RoundTrip(new NetworkBattleSupplyProgress(
            "rt_battle", new[] { new SupplyProgressEntry("party", 2) }));
        Assert.False(legacyReport.IsFlush);
    }

    // Serializes through the production protobuf serializer and reads the copy back.
    private static T RoundTrip<T>(T message)
    {
        Assert.True(RuntimeTypeModel.Default.CanSerialize(typeof(T)));
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        return serializer.Deserialize<T>(serializer.Serialize(message));
    }
}
