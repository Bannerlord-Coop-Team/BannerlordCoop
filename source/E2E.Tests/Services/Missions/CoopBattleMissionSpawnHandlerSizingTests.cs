using Missions.Battles;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Game-independent tests for <see cref="CoopBattleMissionSpawnHandler.DecideJointSizing"/>: sizing waits until
/// both reserves land (the joint cap needs both totals) and only runs Init on a positive combined total.
/// </summary>
public class CoopBattleMissionSpawnHandlerSizingTests
{
    [Fact]
    public void OneSideNotPopulated_NotReady_HoldsBothSides()
    {
        // Own side already has its reserve but the enemy side's (empty) reserve is still in flight: not ready,
        // so both sides stay held at zero until the second reserve lands.
        var decision = CoopBattleMissionSpawnHandler.DecideJointSizing(
            defenderPopulated: true, attackerPopulated: false, defenderOwned: 7, attackerOwned: 0);

        Assert.False(decision.Ready);
        Assert.False(decision.SizeNow);
    }

    [Fact]
    public void NeitherPopulated_NotReady()
    {
        var decision = CoopBattleMissionSpawnHandler.DecideJointSizing(
            defenderPopulated: false, attackerPopulated: false, defenderOwned: 0, attackerOwned: 0);

        Assert.False(decision.Ready);
        Assert.False(decision.SizeNow);
    }

    [Fact]
    public void BothPopulated_WithTroops_SizesJointly()
    {
        // A non-host: own defender side owns troops, enemy attacker side is an empty (but populated) reserve.
        var decision = CoopBattleMissionSpawnHandler.DecideJointSizing(
            defenderPopulated: true, attackerPopulated: true, defenderOwned: 7, attackerOwned: 0);

        Assert.True(decision.Ready);
        Assert.True(decision.SizeNow);
    }

    [Fact]
    public void BothPopulated_BothEmpty_ReadyButDoesNotRunInit()
    {
        // Defensive: both sides owning nothing must not hand Init a 0/0 total (which would divide by zero).
        var decision = CoopBattleMissionSpawnHandler.DecideJointSizing(
            defenderPopulated: true, attackerPopulated: true, defenderOwned: 0, attackerOwned: 0);

        Assert.True(decision.Ready);
        Assert.False(decision.SizeNow);
    }
}
