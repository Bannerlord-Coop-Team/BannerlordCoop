using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>Retained-hero identity, authority handoff, and supplied-origin regression tests without native agents.</summary>
public class RetainedPlayerHeroTests
{
    [Fact]
    public void Return_MovesRiderAndMountOnce_PreservingProvenanceAndMovementIdentity()
    {
        using var registry = NewRegistry();
        var rider = Guid.NewGuid();
        var mount = Guid.NewGuid();
        Register(registry, rider, 7, 1);
        Register(registry, mount, 8, 3);

        Assert.True(registry.TryReturnRetainedPlayer("holder", "returner", rider, 1, mount, 3));
        Assert.True(registry.TryReturnRetainedPlayer("holder", "returner", rider, 1, mount, 3));
        Assert.Empty(registry.GetAgents("holder"));
        Assert.Equal(2, registry.GetAgents("returner").Count);
        Assert.True(registry.TryGetAgentInfo(rider, out var hero));
        Assert.Equal(2, hero.AuthorityRevision);
        Assert.Equal("origin", hero.OriginalOwner);
        Assert.Equal("origin:mission-1", hero.MovementScopeId);
        Assert.Equal(7, hero.MovementId);
        Assert.True(registry.TryGetAgentInfo(mount, out var horse));
        Assert.Equal(4, horse.AuthorityRevision);
    }

    [Theory]
    [InlineData("holder", 2)]
    [InlineData("successor", 1)]
    [InlineData("returner", 1)]
    public void Return_RefusesChangedMountWithoutPartiallyMovingRider(string mountOwner, long revision)
    {
        using var registry = NewRegistry();
        var rider = Guid.NewGuid();
        var mount = Guid.NewGuid();
        Register(registry, rider, 7, 1);
        Register(registry, mount, 8, revision, mountOwner);

        Assert.False(registry.TryReturnRetainedPlayer("holder", "returner", rider, 1, mount, 1));
        Assert.True(registry.TryGetAgentInfo(rider, out var hero));
        Assert.Equal("holder", hero.CurrentAuthority);
        Assert.Equal(1, hero.AuthorityRevision);
    }

    [Fact]
    public void DelayedReturn_CannotUndoLaterMigrationOrReplaceAnEqualRevisionOwner()
    {
        using var registry = NewRegistry();
        var rider = Guid.NewGuid();
        Register(registry, rider, 7, 1);
        Assert.True(registry.TryTransferAuthority("successor", rider));
        Assert.False(registry.TryReturnRetainedPlayer("holder", "returner", rider, 1, Guid.Empty, 0));
        Assert.False(registry.TryReturnRetainedPlayer("holder", "returner", rider, 2, Guid.Empty, 0));
        Assert.True(registry.TryGetAgentInfo(rider, out var hero));
        Assert.Equal("successor", hero.CurrentAuthority);
        Assert.Equal(2, hero.AuthorityRevision);
    }

    [Fact]
    public void Return_RefusesMissingAgentAndRevisionOverflow()
    {
        using var registry = NewRegistry();
        var rider = Guid.NewGuid();
        Assert.False(registry.TryReturnRetainedPlayer("holder", "returner", rider, 1, Guid.Empty, 0));
        Register(registry, rider, 7, long.MaxValue);
        Assert.False(registry.TryReturnRetainedPlayer("holder", "returner", rider, long.MaxValue, Guid.Empty, 0));
    }

    [Theory]
    [InlineData("player-party", "hero", 1141, true)]
    [InlineData("other-party", "hero", 1141, false)]
    [InlineData("player-party", "other-hero", 1141, false)]
    [InlineData("player-party", "hero", 1142, false)]
    [InlineData("player-party", "remaining-troop", 1142, false)]
    public void SuppliedOrigin_RequiresExactIdentityWithoutConsumingAnotherTroop(
        string party, string character, int seed, bool expected)
    {
        var supplier = Supplier(new[] { new TroopReserveEntry(1141, "hero", 0), new TroopReserveEntry(1142, "remaining-troop", 0) });
        Assert.Equal(expected, supplier.IsTroopAlreadySupplied(party, character, seed));
        Assert.Equal(1, supplier.GetRemainingForParty("player-party"));
        Assert.Equal(1, supplier.CaptureAllocationSnapshot().SuppliedTroops);
    }

    [Fact]
    public void ExhaustedOneEntryOrigin_RemainsSuppliedAndMatchesOnlyThePlayerSide()
    {
        var supplier = Supplier(new[] { new TroopReserveEntry(1141, "hero", 0) });
        var other = new CoopTroopSupplier("battle", BattleSideEnum.Defender, null, new BattleAgentBudget());
        var handler = new CoopBattleMissionSpawnHandler(other, supplier, null, BattleSideEnum.Attacker);
        Assert.True(handler.HasSuppliedPlayerOrigin(Record()));
        Assert.False(CoopBattleMissionSpawnHandler.HasLocalPlayerOrigin(BattleSideEnum.Attacker, "player-party", other, supplier));
        Assert.Equal(0, supplier.GetRemainingForParty("player-party"));
        Assert.Equal(1, supplier.CaptureAllocationSnapshot().SuppliedTroops);
    }

    [Fact]
    public void ReturnedRecord_PreservesOriginalSnapshotAndAdvancesOnlyGrantedAuthorities()
    {
        var original = Record();
        var handoff = new NetworkRetainedPlayerHero("battle", 2, "returner", original);
        var returned = handoff.CreateReturnedRecord();
        Assert.Equal(original.AgentId, returned.AgentId);
        Assert.Equal(original.CharacterId, returned.CharacterId);
        Assert.Equal(original.MapEventPartyId, returned.MapEventPartyId);
        Assert.Equal(original.TroopSeed, returned.TroopSeed);
        Assert.Equal(original.Position, returned.Position);
        Assert.Equal(original.Health, returned.Health);
        Assert.Same(original.SpawnEquipment, returned.SpawnEquipment);
        Assert.Equal(original.OriginalOwnerControllerId, returned.OriginalOwnerControllerId);
        Assert.Equal(original.MovementScopeId, returned.MovementScopeId);
        Assert.Equal(original.MovementId, returned.MovementId);
        Assert.Equal("returner", returned.OwnerControllerId);
        Assert.Equal(2, returned.AuthorityRevision);
        Assert.Equal("holder", original.OwnerControllerId);
        Assert.Equal(1, original.AuthorityRevision);
    }

    private static NetworkAgentRegistry NewRegistry()
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(value => value.ControllerId).Returns("returner");
        return new NetworkAgentRegistry(provider.Object);
    }

    private static void Register(NetworkAgentRegistry registry, Guid id, ushort movementId, long revision, string owner = "holder")
    {
        Assert.True(registry.TryRegisterAgent(owner, "origin", "origin:mission-1", id,
            movementId, ObjectHelper.SkipConstructor<Agent>(), revision));
    }

    private static CoopTroopSupplier Supplier(TroopReserveEntry[] entries)
    {
        var supplier = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, null, new BattleAgentBudget());
        supplier.SetReserve(new[] { new PartyReserve("player-party", 1, entries, isReceiverPlayerParty: true) },
            entries.Length, 1, 1000, 8);
        return supplier;
    }

    private static BattleAgentSpawnData Record() => new BattleAgentSpawnData(
        Guid.NewGuid(), "hero", new Vec3(1, 2, 3), BattleSideEnum.Attacker, 22,
        "holder", "player-party", 1141, new Equipment(), default, null,
        movementId: 7, originalOwnerControllerId: "origin", movementScopeId: "origin:mission-1", authorityRevision: 1);
}
