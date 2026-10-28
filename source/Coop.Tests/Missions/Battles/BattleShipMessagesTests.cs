using Missions.Battles;
using Missions.Messages;
using ProtoBuf;
using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The hull messages' wire round trips and the value rules they carry (crew load, condition tolerance).</summary>
public class BattleShipMessagesTests
{
    private const string Peer = "peer";

    [Fact]
    public void Removal_KeepsWhetherTheOwnersMissionEndedOverTheWire()
    {
        var shipId = Guid.NewGuid();

        var ending = Serializer.DeepClone(new NetworkShipRemoved(shipId, Peer, ShipRemovalReason.Leave, missionEnding: true));
        var staying = Serializer.DeepClone(new NetworkShipRemoved(shipId, Peer, ShipRemovalReason.Retreat, missionEnding: false));

        Assert.True(ending.MissionEnding);
        Assert.Equal(ShipRemovalReason.Leave, ending.Reason);
        Assert.False(staying.MissionEnding);
        Assert.Equal(shipId, staying.ShipId);
    }

    [Fact]
    public void Sample_RoundTripsTheHelmInput()
    {
        var input = new BattleShipInput(1, 2, 0, -0.5f, 2);
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), input);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, sample);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkBattleShipSample>(stream);

        Assert.Equal(input, copy.Input);
        Assert.Equal(sample.Frame, copy.Frame);
        Assert.Equal(7, copy.Sequence);
    }

    // M10: copies extrapolate the owner's frame with the body velocity, yaw rate and roll rate the sample carries.
    [Fact]
    public void Sample_RoundTripsTheOwnersBodyMotion()
    {
        var motion = new BattleShipMotion(new Vec2(4.5f, -1.25f), 0.2f, rollRate: -0.07f);
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), motion: motion);

        var copy = Serializer.DeepClone(sample);

        Assert.Equal(4.5f, copy.Motion.Velocity.x);
        Assert.Equal(-1.25f, copy.Motion.Velocity.y);
        Assert.Equal(0.2f, copy.Motion.YawRate);
        Assert.Equal(-0.07f, copy.Motion.RollRate);
    }

    // R45 roll: copies heel under the owner's crew load, which rides each sample as mass and a hull-local centre.
    [Fact]
    public void Sample_RoundTripsTheOwnersCrewLoad()
    {
        var load = BattleShipCrewLoad.Create(480f, new Vec3(0.75f, -6.5f, 2.25f));
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), motion: new BattleShipMotion(new Vec2(1f, 2f), 0.1f, load));

        var copy = Serializer.DeepClone(sample);

        Assert.Equal(480f, copy.Motion.CrewLoad.Mass);
        Assert.Equal(0.75f, copy.Motion.CrewLoad.LocalCentre.x);
        Assert.Equal(-6.5f, copy.Motion.CrewLoad.LocalCentre.y);
        Assert.Equal(2.25f, copy.Motion.CrewLoad.LocalCentre.z);
    }

    // A sample without a load (a frame that committed no weight while agents stood aboard) writes no crew fields.
    [Fact]
    public void Sample_WithoutACrewLoad_ArrivesAsUnknown()
    {
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), motion: new BattleShipMotion(new Vec2(1f, 2f), 0.1f));

        var copy = Serializer.DeepClone(sample);

        Assert.False(copy.HasCrewLoad);
        Assert.Null(copy.Motion.CrewLoad);
    }

    // A crew that left the deck (all boarded an enemy hull) is a known-empty load, not an unknown one.
    [Fact]
    public void Sample_RoundTripsAKnownEmptyDeck()
    {
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity),
            motion: new BattleShipMotion(new Vec2(1f, 2f), 0.1f, BattleShipCrewLoad.Empty));

        var copy = Serializer.DeepClone(sample);

        Assert.True(copy.HasCrewLoad);
        Assert.Same(BattleShipCrewLoad.Empty, copy.Motion.CrewLoad);
    }

    [Fact]
    public void CrewLoad_IsEmptyForAZeroMass()
    {
        Assert.Same(BattleShipCrewLoad.Empty, BattleShipCrewLoad.Create(0f, new Vec3(1f, 2f, 3f)));
        Assert.True(BattleShipCrewLoad.Empty.IsEmpty);
    }

    [Theory]
    [InlineData(-80f, 0f, 0f, 0f)]
    [InlineData(float.NaN, 0f, 0f, 0f)]
    [InlineData(float.PositiveInfinity, 0f, 0f, 0f)]
    [InlineData(80f, float.NaN, 0f, 0f)]
    [InlineData(80f, 0f, float.NegativeInfinity, 0f)]
    [InlineData(80f, 0f, 0f, float.NaN)]
    public void CrewLoad_IsUnknownForANegativeOrNonFiniteLoad(float mass, float x, float y, float z)
    {
        Assert.Null(BattleShipCrewLoad.Create(mass, new Vec3(x, y, z)));
    }

    [Fact]
    public void SpawnRecord_RoundTripsTheHullDescriptor()
    {
        var record = new BattleShipSpawnData(Guid.NewGuid(), Peer, "MapEventParty_1", false, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", new[] { "sail" },
            new[] { "sail_piece" }, "figurehead_lion", "Sea Wolf", 900f, 400f, 1234, "pattern");

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkSpawnBattleShips(new[] { record }));
        stream.Position = 0;
        var copy = Assert.Single(Serializer.Deserialize<NetworkSpawnBattleShips>(stream).Ships);

        Assert.Equal(record.ShipId, copy.ShipId);
        Assert.Equal(record.OwnerControllerId, copy.OwnerControllerId);
        Assert.Equal(record.Side, copy.Side);
        Assert.Equal(record.FormationIndex, copy.FormationIndex);
        Assert.Equal(record.Frame, copy.Frame);
        Assert.Equal(record.HullId, copy.HullId);
        Assert.Equal(record.PieceSlots, copy.PieceSlots);
        Assert.Equal(record.PieceIds, copy.PieceIds);
        Assert.Equal(record.FigureheadId, copy.FigureheadId);
        Assert.Equal(record.Name, copy.Name);
        Assert.Equal(record.HitPoints, copy.HitPoints);
        Assert.Equal(record.SailHitPoints, copy.SailHitPoints);
        Assert.Equal(record.RandomValue, copy.RandomValue);
        Assert.Equal(record.CustomSailPatternId, copy.CustomSailPatternId);
    }

    // An AI hull a later host simulates keeps naming the host that fielded it.
    [Fact]
    public void SpawnRecord_RoundTripsTheOriginalOwner_AndAnOwnRecordNamesItsOwner()
    {
        var migrated = new BattleShipSpawnData(Guid.NewGuid(), "host", "MapEventParty_1", true, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Peer);
        var own = new BattleShipSpawnData(Guid.NewGuid(), Peer, "MapEventParty_1", false, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Peer);

        var copies = Serializer.DeepClone(new NetworkSpawnBattleShips(new[] { migrated, own })).Ships;

        Assert.Equal(Peer, copies[0].OriginalOwner);
        Assert.Equal(Peer, copies[1].OriginalOwner);
        Assert.Null(copies[1].OriginalOwnerControllerId);
    }

    // M6 D1: a copy needs its campaign ship's id to report the ship's fate after it becomes the hull's authority.
    [Fact]
    public void SpawnRecord_RoundTripsTheCampaignShipId()
    {
        var record = new BattleShipSpawnData(Guid.NewGuid(), "host", "MapEventParty_1", true, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Peer, "Ship_7");
        var withoutShip = new BattleShipSpawnData(Guid.NewGuid(), Peer, "MapEventParty_1", false, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "");

        var copies = Serializer.DeepClone(new NetworkSpawnBattleShips(new[] { record, withoutShip })).Ships;

        Assert.Equal("Ship_7", copies[0].CampaignShipId);
        Assert.Null(copies[1].CampaignShipId);
    }

    [Fact]
    public void Condition_DiffersOnlyBeyondTheToleranceOrOnSinkingState()
    {
        var condition = new BattleShipCondition(500f, 200f, 100f, new[] { 50f, 50f }, 0);

        Assert.True(condition.DiffersFrom(null));
        Assert.False(condition.DiffersFrom(new BattleShipCondition(500.005f, 200f, 100f, new[] { 50f, 50f }, 0)));
        Assert.True(condition.DiffersFrom(new BattleShipCondition(500f, 200f, 100f, new[] { 50f, 49f }, 0)));
        Assert.True(condition.DiffersFrom(new BattleShipCondition(500f, 200f, 100f, new[] { 50f, 50f }, 1)));
    }

    [Fact]
    public void NetworkShipCondition_RoundTripsEveryField()
    {
        var message = new NetworkShipCondition(Guid.NewGuid(), Peer, 7, 2,
            new BattleShipCondition(420.5f, 80f, -3f, new[] { 1f, 2f, 3f, 4f, 5f, 6f }, 2));

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkShipCondition>(stream);

        Assert.Equal(message.ShipId, copy.ShipId);
        Assert.Equal(Peer, copy.OwnerControllerId);
        Assert.Equal(7, copy.Revision);
        Assert.Equal(2, copy.HostEpoch);
        Assert.Equal(420.5f, copy.Condition.HitPoints);
        Assert.Equal(80f, copy.Condition.SailHitPoints);
        Assert.Equal(-3f, copy.Condition.FireHitPoints);
        Assert.Equal(message.Condition.PartialHitPoints, copy.Condition.PartialHitPoints);
        Assert.Equal(2, copy.Condition.SinkingState);
    }
}
