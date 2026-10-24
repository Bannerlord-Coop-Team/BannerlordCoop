using Missions.Battles;
using Missions.Messages;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The condition stream's validation rules; its send and apply paths are driven through the replicator's tests.</summary>
public class BattleShipConditionStreamTests
{
    private const string Own = "local";
    private const string Peer = "peer";
    private static readonly string[] InMission = { Peer, "someone_else" };

    [Fact]
    public void Condition_KeepsItsReserveOverTheWire_AndANegativeReserveIsRejected()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, CreateHull(), null);

        var wire = Serializer.DeepClone(new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(100f), 14));

        Assert.Equal(14, wire.SpawnableReserve);
        Assert.Equal("invalid_reserve", BattleShipConditionStream.ValidateCondition(ship, Own,
            new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(100f), -1), 0, 0, InMission));
    }

    [Theory]
    [InlineData(Peer, Peer, false, 5, 0, 4, 0, null)]
    [InlineData(Peer, "someone_else", false, 5, 0, 4, 0, "not_authority")]
    [InlineData(Own, Own, false, 5, 0, 4, 0, "own_hull")]
    [InlineData(Peer, Peer, false, 4, 0, 4, 0, "stale")]
    [InlineData(Peer, Peer, false, 3, 9, 4, 1, "stale")]
    [InlineData(Peer, Peer, true, 9, 2, 1, 3, "stale_epoch")]
    [InlineData(Peer, Peer, true, 1, 4, 9, 3, null)]
    [InlineData(Peer, Peer, true, 9, 3, 9, 3, "stale")]
    public void ValidateCondition_KeepsTheOwnersRevisionOrderAndRestartsItForANewHostEpoch(string authority, string sender,
        bool isNpcParty, long revision, int epoch, long acceptedRevision, int acceptedEpoch, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, "MapEventParty_9", isNpcParty, CreateHull(), null);
        var condition = new NetworkShipCondition(ship.ShipId, sender, revision, epoch, Condition(500f));

        Assert.Equal(expected, BattleShipConditionStream.ValidateCondition(ship, Own, condition, acceptedRevision, acceptedEpoch, InMission));
    }

    [Fact]
    public void ValidateCondition_RejectsANonFiniteCondition()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        var condition = new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(float.NaN));

        Assert.Equal("invalid_condition", BattleShipConditionStream.ValidateCondition(ship, Own, condition, 0, 0, InMission));
    }

    private static BattleShipCondition Condition(float hitPoints) =>
        new BattleShipCondition(hitPoints, 300f, 100f, new[] { 80f, 80f, 80f, 80f, 80f, 80f }, 0);

    private static MissionObject CreateHull() => ShipTestHulls.Create();
}
