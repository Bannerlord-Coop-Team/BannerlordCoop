using GameInterface.Services.MapEvents.TroopSupply;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.TroopSupply;

public class CoopTroopSupplierRegistryTests
{
    [Fact]
    public void Register_AfterPendingReplacementUsesLatestReserveAndGrantMetadata()
    {
        const string mapEventId = "pending-initial-spawn";
        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        try
        {
            CoopTroopSupplierRegistry.Feed(mapEventId, BattleSideEnum.Attacker,
                new[] { Reserve(entryCount: 100, initialSpawnCount: 25) },
                grantGeneration: 41,
                completesInitialSizing: false);
            CoopTroopSupplierRegistry.Feed(mapEventId, BattleSideEnum.Attacker,
                new[] { Reserve(entryCount: 80, initialSpawnCount: 0) },
                grantGeneration: 42,
                completesInitialSizing: true);

            var supplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, null!);
            CoopTroopSupplierRegistry.Register(supplier);

            Assert.Equal(0, supplier.InitialTroops);
            Assert.Equal(80, supplier.TotalTroops);
            var sizing = supplier.GetSizingSnapshot();
            Assert.Equal(42, sizing.GrantGeneration);
            Assert.True(sizing.CompletesInitialSizing);
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    private static PartyReserve Reserve(int entryCount, int initialSpawnCount)
        => new PartyReserve("party-1", suppliedCount: 0,
            new TroopReserveEntry[entryCount], initialSpawnCount);
}
