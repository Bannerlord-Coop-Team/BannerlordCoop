using Missions.Battles;
using Missions.Messages;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NetworkShipRegistryTests
{
    [Fact]
    public void TryRegister_FindsTheHullByIdAndObject()
    {
        var registry = new NetworkShipRegistry();
        var hull = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "local", "MapEventParty_1", false, hull, null);

        Assert.True(registry.TryRegister(ship));

        Assert.True(registry.TryGet(ship.ShipId, out var byId));
        Assert.Same(ship, byId);
        Assert.True(registry.TryGetByHull(hull, out var byHull));
        Assert.Same(ship, byHull);
        Assert.Equal("local", byId.CurrentAuthority);
        Assert.Equal("local", byId.OriginalOwner);
    }

    [Fact]
    public void TryRegister_SameHullTwice_KeepsTheFirstIdentity()
    {
        var registry = new NetworkShipRegistry();
        var hull = CreateHull();
        var first = new NetworkShipInfo(Guid.NewGuid(), "local", null, false, hull, null);

        Assert.True(registry.TryRegister(first));
        Assert.False(registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), "local", null, false, hull, null)));
        Assert.Same(first, Assert.Single(registry.Ships));
    }

    [Fact]
    public void TryRebindHull_KeepsTheShipIdentityOnTheReplacementHull()
    {
        var registry = new NetworkShipRegistry();
        var copy = CreateHull();
        var fresh = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "host", "MapEventParty_9", true, copy, null);
        registry.TryRegister(ship);
        ship.CurrentAuthority = "successor";

        Assert.True(registry.TryRebindHull(ship.ShipId, fresh));

        Assert.True(registry.TryGetByHull(fresh, out var byFresh));
        Assert.Same(ship, byFresh);
        Assert.False(registry.TryGetByHull(copy, out _));
        Assert.Equal("successor", byFresh.CurrentAuthority);
        Assert.Equal("host", byFresh.OriginalOwner);
        Assert.Equal("MapEventParty_9", byFresh.MapEventPartyId);
        Assert.True(byFresh.IsNpcParty);
    }

    [Fact]
    public void TryRebindHull_RefusesAHullAnotherShipHoldsOrAnUnknownShip()
    {
        var registry = new NetworkShipRegistry();
        var first = new NetworkShipInfo(Guid.NewGuid(), "host", null, true, CreateHull(), null);
        var second = new NetworkShipInfo(Guid.NewGuid(), "host", null, true, CreateHull(), null);
        registry.TryRegister(first);
        registry.TryRegister(second);

        Assert.False(registry.TryRebindHull(first.ShipId, second.Hull));
        Assert.False(registry.TryRebindHull(Guid.NewGuid(), CreateHull()));
        Assert.False(registry.TryRebindHull(first.ShipId, null));
    }

    [Fact]
    public void MarkSpawnFailed_HoldsUntilTheHullRegisters()
    {
        var registry = new NetworkShipRegistry();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "host", "MapEventParty_9", true, CreateHull(), null);

        registry.MarkSpawnFailed(ship.ShipId);
        bool failedBeforeRegister = registry.StateOf(ship.ShipId) == ShipLifecycle.SpawnFailed;
        registry.TryRegister(ship);
        registry.MarkSpawnFailed(ship.ShipId);

        Assert.True(failedBeforeRegister);
        Assert.False(registry.StateOf(ship.ShipId) == ShipLifecycle.SpawnFailed);
    }

    [Fact]
    public void StateOf_FollowsAForeignHullFromUnknownToRegisteredAndRemoved()
    {
        var registry = new NetworkShipRegistry();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "peer", "MapEventParty_2", false, CreateHull(), null);
        var unknown = registry.StateOf(ship.ShipId);

        registry.TryRegister(ship);
        registry.MarkSpawnFailed(ship.ShipId);
        var registered = registry.StateOf(ship.ShipId);
        registry.Remove(ship.ShipId);

        Assert.Equal(ShipLifecycle.Unknown, unknown);
        Assert.Equal(ShipLifecycle.Registered, registered);
        Assert.Equal(ShipLifecycle.Removed, registry.StateOf(ship.ShipId));
    }

    [Fact]
    public void StateOf_AWaitingRecordGivenUpOnReadsSpawnFailed_AndRemovingAnUnknownShipChangesNothing()
    {
        var registry = new NetworkShipRegistry();
        var shipId = Guid.NewGuid();

        registry.MarkSpawnFailed(shipId);

        Assert.Equal(ShipLifecycle.SpawnFailed, registry.StateOf(shipId));
        Assert.False(registry.Remove(shipId));
        Assert.Equal(ShipLifecycle.SpawnFailed, registry.StateOf(shipId));
    }

    // M8a: a hull that left with its owner stays gone; a late record or a retry must not make it wait or spawn again.
    [Fact]
    public void MarkRemoved_SticksOverALaterWaitOrFailure_AndReadsGone()
    {
        var registry = new NetworkShipRegistry();
        var shipId = Guid.NewGuid();
        bool goneWhileUnknown = registry.IsGone(shipId);

        registry.MarkRemoved(shipId);
        registry.RetrySpawn(shipId);
        registry.MarkSpawnFailed(shipId);

        Assert.False(goneWhileUnknown);
        Assert.Equal(ShipLifecycle.Removed, registry.StateOf(shipId));
        Assert.True(registry.IsGone(shipId));
    }

    // A fresh record of a ship whose spawn was given up makes its buffers wait for the hull again.
    [Fact]
    public void RetrySpawn_ClearsAGivenUpSpawn_AndNothingElse()
    {
        var registry = new NetworkShipRegistry();
        var failed = Guid.NewGuid();
        var removed = Guid.NewGuid();
        registry.MarkSpawnFailed(failed);
        registry.MarkRemoved(removed);

        registry.RetrySpawn(failed);
        registry.RetrySpawn(removed);

        Assert.Equal(ShipLifecycle.Unknown, registry.StateOf(failed));
        Assert.False(registry.IsGone(failed));
        Assert.Equal(ShipLifecycle.Removed, registry.StateOf(removed));
    }

    [Fact]
    public void IsGone_HoldsForAGivenUpOrRemovedShip_NotForAWaitingOrRegisteredOne()
    {
        var registry = new NetworkShipRegistry();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "peer", "MapEventParty_2", false, CreateHull(), null);
        var failed = Guid.NewGuid();
        registry.MarkSpawnFailed(failed);
        registry.TryRegister(ship);
        bool goneWhileRegistered = registry.IsGone(ship.ShipId);

        registry.Remove(ship.ShipId);

        Assert.True(registry.IsGone(failed));
        Assert.False(goneWhileRegistered);
        Assert.True(registry.IsGone(ship.ShipId));
        Assert.False(registry.IsGone(Guid.NewGuid()));
    }

    // Capture 2.3 step 3: crew identity is formation-based, so after vanilla's swap the captor's formation names the prize,
    // whose crew and reinforcements it now holds, and the captor's old hull answers for no formation.
    [Fact]
    public void MarkCapturedAndOrphaned_MapTheCaptorFormationToThePrize_AndNothingToTheOrphan()
    {
        var registry = new NetworkShipRegistry();
        var captorFormation = CreateFormation();
        var prizeFormation = CreateFormation();
        var prize = new NetworkShipInfo(Guid.NewGuid(), "host", "MapEventParty_2", true, CreateHull(), prizeFormation);
        var captorShip = new NetworkShipInfo(Guid.NewGuid(), "captor", "MapEventParty_1", false, CreateHull(), captorFormation);
        registry.TryRegister(prize);
        registry.TryRegister(captorShip);

        Assert.True(registry.TryMarkCaptured(prize.ShipId, new HullCapture("captor", captorShip.ShipId, BattleSideEnum.Defender),
            captorShip.Formation, 1));
        Assert.True(registry.TryMarkOrphaned(captorShip.ShipId, BattleSideEnum.Attacker, 1));

        Assert.True(registry.TryGetByFormation(captorFormation, out var crewShip));
        Assert.Same(prize, crewShip);
        Assert.False(registry.TryGetByFormation(prizeFormation, out _));
        Assert.Null(captorShip.Formation);
        Assert.True(captorShip.IsOrphaned);
        Assert.Equal(BattleSideEnum.Attacker, captorShip.OrphanedSide);
        Assert.Equal("captor", prize.CaptorControllerId);
        Assert.False(prize.IsOrphaned);
        Assert.Equal(1, prize.CaptureSequence);
        Assert.False(registry.TryMarkOrphaned(prize.ShipId, BattleSideEnum.None, 2));
    }

    // A captured AI hull stays with its captor through host changes, migration and NPC reserve fielding (K8).
    [Fact]
    public void FollowsBattleHost_IsFalseOnceAnAiHullIsCaptured()
    {
        var registry = new NetworkShipRegistry();
        var prize = new NetworkShipInfo(Guid.NewGuid(), "host", "MapEventParty_2", true, CreateHull(), CreateFormation());
        registry.TryRegister(prize);
        bool followedBefore = prize.FollowsBattleHost;

        registry.TryMarkCaptured(prize.ShipId, new HullCapture("captor", Guid.NewGuid(), BattleSideEnum.Defender), CreateFormation(), 1);

        Assert.True(followedBefore);
        Assert.False(prize.FollowsBattleHost);
    }

    // A recapture gives an orphan a formation again.
    [Fact]
    public void MarkCaptured_OfAnOrphan_GivesItTheFormation()
    {
        var registry = new NetworkShipRegistry();
        var formation = CreateFormation();
        var own = new NetworkShipInfo(Guid.NewGuid(), "captor", "MapEventParty_1", false, CreateHull(), CreateFormation());
        registry.TryRegister(own);
        registry.TryMarkOrphaned(own.ShipId, BattleSideEnum.Attacker, 1);

        registry.TryMarkCaptured(own.ShipId, new HullCapture("captor", Guid.NewGuid(), BattleSideEnum.None), formation, 2);

        Assert.False(own.IsOrphaned);
        Assert.Same(formation, own.Formation);
        Assert.Equal(2, own.CaptureSequence);
    }

    // C4: only a ship id that left the mission can be reopened, and it then registers again.
    [Fact]
    public void TryReopen_LetsARemovedShipRegisterAgain_AndNothingElse()
    {
        var registry = new NetworkShipRegistry();
        var removed = Guid.NewGuid();
        var failed = Guid.NewGuid();
        registry.MarkRemoved(removed);
        registry.MarkSpawnFailed(failed);

        Assert.True(registry.TryReopen(removed));
        Assert.False(registry.TryReopen(failed));
        Assert.Equal(ShipLifecycle.Unknown, registry.StateOf(removed));
        Assert.True(registry.TryRegister(new NetworkShipInfo(removed, "captor", null, false, CreateHull(), null)));
    }

    // ProtoMember 20 to 24 carry a captured or orphaned hull and its capture count to a late joiner; 18 stays retired.
    [Fact]
    public void SpawnRecord_RoundTripsItsCaptureState()
    {
        var captorShipId = Guid.NewGuid();
        var prize = new BattleShipSpawnData(Guid.NewGuid(), "captor", "MapEventParty_2", true, BattleSideEnum.Attacker, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", "host", "Ship_7", captorControllerId: "captor", captureSequence: 3, capturedWithShipId: captorShipId,
            capturedFromSide: BattleSideEnum.Defender);
        var orphan = new BattleShipSpawnData(captorShipId, "captor", "MapEventParty_1", false, BattleSideEnum.Attacker, 0,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", null, "Ship_8", orphaned: true, captureSequence: 1);

        var copies = ProtoBuf.Serializer.DeepClone(new NetworkSpawnBattleShips(new[] { prize, orphan })).Ships;

        Assert.Equal("captor", copies[0].CaptorControllerId);
        Assert.False(copies[0].FollowsBattleHost);
        Assert.False(copies[0].Orphaned);
        Assert.Equal(3, copies[0].CaptureSequence);
        Assert.Equal(captorShipId, copies[0].CapturedWithShipId);
        Assert.Equal(BattleSideEnum.Defender, copies[0].CapturedFromSide);
        Assert.True(copies[1].Orphaned);
        Assert.Null(copies[1].CaptorControllerId);
        Assert.Equal(1, copies[1].CaptureSequence);
    }
    private static MissionObject CreateHull() => ShipTestHulls.Create();

    private static Formation CreateFormation() => (Formation)RuntimeHelpers.GetUninitializedObject(typeof(Formation));
}
