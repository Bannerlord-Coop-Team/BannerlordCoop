using Common.Util;
using GameInterface.Services.MapEvents.TroopSupply;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.TroopSupply;

public class CoopAgentOriginTests
{
    [Theory]
    [InlineData(RemovalKind.Wounded)]
    [InlineData(RemovalKind.Killed)]
    [InlineData(RemovalKind.Routed)]
    public void CasualtyCallback_NotifiesOwnerSupplierOnlyOnce(RemovalKind removalKind)
    {
        var supplier = new CoopTroopSupplier("map-event-1", BattleSideEnum.Attacker, null!);
        supplier.SetReserve(new[]
        {
            new PartyReserve(
                "map-event-party-1",
                suppliedCount: 0,
                new[] { new TroopReserveEntry(42, "troop-1", formationClass: 0) },
                initialSpawnCount: 1),
        });
        var origin = CreateOrigin(supplier);

        ApplyRemoval(origin, removalKind);
        ApplyRemoval(origin, removalKind);
        origin.SetWounded();
        origin.SetKilled();
        origin.SetRouted(isOrderRetreat: false);

        Assert.Equal(1, supplier.NumRemovedTroops);
    }

    [Fact]
    public void CasualtyCallbacks_UnboundPuppetOrigin_DoNothing()
    {
        var origin = CreateOrigin(null);

        var exception = Record.Exception(() =>
        {
            origin.SetKilled();
            origin.SetWounded();
            origin.SetRouted(isOrderRetreat: false);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void SuppressRemoval_PreventsAdministrativeDespawnFromAdvancingQuota()
    {
        var supplier = new CoopTroopSupplier("map-event-1", BattleSideEnum.Attacker, null!);
        supplier.SetReserve(new[]
        {
            new PartyReserve(
                "map-event-party-1",
                suppliedCount: 0,
                new[] { new TroopReserveEntry(42, "troop-1", formationClass: 0) },
                initialSpawnCount: 1),
        });
        var origin = CreateOrigin(supplier);

        origin.SuppressRemoval();
        origin.SetRouted(isOrderRetreat: false);

        Assert.Equal(0, supplier.NumRemovedTroops);
    }

    [Fact]
    public void CreateRetryOrigin_HasIndependentRemovalLatch()
    {
        var supplier = new CoopTroopSupplier("map-event-1", BattleSideEnum.Attacker, null!);
        supplier.SetReserve(new[]
        {
            new PartyReserve(
                "map-event-party-1",
                suppliedCount: 0,
                new[] { new TroopReserveEntry(42, "troop-1", formationClass: 0) },
                initialSpawnCount: 1),
        });
        var retiredOrigin = CreateOrigin(supplier);

        retiredOrigin.SuppressRemoval();
        var retryOrigin = retiredOrigin.CreateRetryOrigin();
        retiredOrigin.SetKilled();
        retryOrigin.SetKilled();

        Assert.Equal(1, supplier.NumRemovedTroops);
    }

    private static CoopAgentOrigin CreateOrigin(CoopTroopSupplier? supplier)
    {
        var troop = ObjectHelper.SkipConstructor<CharacterObject>();
        return new CoopAgentOrigin(
            troop, null!, 0, null!, new UniqueTroopDescriptor(42), "map-event-party-1", supplier);
    }

    private static void ApplyRemoval(CoopAgentOrigin origin, RemovalKind removalKind)
    {
        if (removalKind == RemovalKind.Wounded) origin.SetWounded();
        else if (removalKind == RemovalKind.Killed) origin.SetKilled();
        else origin.SetRouted(isOrderRetreat: false);
    }

    public enum RemovalKind
    {
        Wounded,
        Killed,
        Routed,
    }
}
