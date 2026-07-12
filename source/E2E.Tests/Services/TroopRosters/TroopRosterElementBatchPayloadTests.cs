using GameInterface.Services.TroopRosters.Coalescing;
using GameInterface.Services.TroopRosters.Messages;

namespace E2E.Tests.Services.TroopRosters;

/// <summary>
/// Covers the safe compaction rules for ordered troop-roster element batches.
/// </summary>
public class TroopRosterElementBatchPayloadTests
{
    [Theory]
    [InlineData("GameInterface.Services.TroopRosters.Messages.NetworkTroopRosterAddCounts")]
    [InlineData("GameInterface.Services.TroopRosters.Messages.NetworkTroopRosterSetXp")]
    public void LegacySingleOperationMessages_AreNotInAssembly(string fullName)
    {
        Assert.Null(typeof(NetworkTroopRosterElementBatch).Assembly.GetType(fullName));
    }

    [Fact]
    public void Merge_AdjacentXpSets_KeepsLatestOnly()
    {
        var payload = new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.SetXp(10));

        payload.Merge(new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.SetXp(20)));

        var batch = Assert.IsType<NetworkTroopRosterElementBatch>(payload.ToMessage());
        var operation = Assert.Single(batch.Operations);
        Assert.Equal(TroopRosterElementOperationKind.SetXp, operation.Kind);
        Assert.Equal(20, operation.Xp);
    }

    [Fact]
    public void Merge_AdjacentAddCounts_PreservesEveryNonCommutativeOperation()
    {
        var payload = new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.AddCounts(5, 5, 100, true));

        payload.Merge(new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.AddCounts(-4, 0, 50, true)));
        payload.Merge(new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.AddCounts(4, 0, 7, false)));

        var batch = Assert.IsType<NetworkTroopRosterElementBatch>(payload.ToMessage());
        Assert.Collection(batch.Operations,
            first => AssertAddCounts(first, 5, 5, 100, true),
            second => AssertAddCounts(second, -4, 0, 50, true),
            third => AssertAddCounts(third, 4, 0, 7, false));
    }

    [Fact]
    public void Merge_AddCountsBetweenXpSets_PreservesOperationOrderAndArguments()
    {
        var payload = new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.SetXp(10));

        payload.Merge(new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.AddCounts(-3, -1, 7, true)));
        payload.Merge(new TroopRosterElementBatchPayload("roster", "character",
            TroopRosterElementOperation.SetXp(20)));

        var batch = Assert.IsType<NetworkTroopRosterElementBatch>(payload.ToMessage());
        Assert.Collection(batch.Operations,
            first =>
            {
                Assert.Equal(TroopRosterElementOperationKind.SetXp, first.Kind);
                Assert.Equal(10, first.Xp);
            },
            second =>
            {
                Assert.Equal(TroopRosterElementOperationKind.AddCounts, second.Kind);
                Assert.Equal(-3, second.Count);
                Assert.Equal(-1, second.WoundedCount);
                Assert.Equal(7, second.Xp);
                Assert.True(second.RemoveDepleted);
            },
            third =>
            {
                Assert.Equal(TroopRosterElementOperationKind.SetXp, third.Kind);
                Assert.Equal(20, third.Xp);
            });
    }

    private static void AssertAddCounts(TroopRosterElementOperation operation, int count,
        int woundedCount, int xp, bool removeDepleted)
    {
        Assert.Equal(TroopRosterElementOperationKind.AddCounts, operation.Kind);
        Assert.Equal(count, operation.Count);
        Assert.Equal(woundedCount, operation.WoundedCount);
        Assert.Equal(xp, operation.Xp);
        Assert.Equal(removeDepleted, operation.RemoveDepleted);
    }
}
