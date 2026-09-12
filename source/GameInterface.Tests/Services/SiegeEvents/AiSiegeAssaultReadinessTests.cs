using GameInterface.Services.SiegeEvents;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class AiSiegeAssaultReadinessTests
{
    private readonly AiSiegeAssaultReadiness readiness = new AiSiegeAssaultReadiness();

    [Fact]
    public void StrongPreparedAttack_WithUsableEquipment_IsViable()
    {
        var result = readiness.Evaluate(new AiSiegeAssaultReadinessInput(
            attackerStrength: 200f,
            defenderStrength: 100f,
            settlementAdvantage: 1f,
            siegeElapsedHours: 96f,
            hasRam: true,
            hasSiegeTower: true,
            equipmentBuilt: 3,
            maximumEquipmentProgress: 0f));

        Assert.True(result.IsViable);
        Assert.Equal(2f, result.PowerRatioBeforeEquipment);
        Assert.True(result.AssaultChance > 0f);
    }

    [Fact]
    public void StrongAttack_WithoutBuiltEquipment_IsNotYetViable()
    {
        var result = readiness.Evaluate(new AiSiegeAssaultReadinessInput(
            attackerStrength: 200f,
            defenderStrength: 100f,
            settlementAdvantage: 1f,
            siegeElapsedHours: 96f,
            hasRam: false,
            hasSiegeTower: false,
            equipmentBuilt: 0,
            maximumEquipmentProgress: 0f));

        Assert.False(result.IsViable);
        Assert.Equal(0f, result.PowerRatioAfterEquipment);
    }

    [Fact]
    public void TimeAfterVanillaGracePeriod_ImprovesPowerRatio()
    {
        var atGracePeriod = readiness.Evaluate(CreateTimedInput(96f));
        var oneDayLater = readiness.Evaluate(CreateTimedInput(120f));

        Assert.True(oneDayLater.PowerRatioBeforeEquipment > atGracePeriod.PowerRatioBeforeEquipment);
    }

    [Fact]
    public void EmptyDefense_IsViableWithoutDividingPolicyFromVanilla()
    {
        var result = readiness.Evaluate(new AiSiegeAssaultReadinessInput(
            attackerStrength: 100f,
            defenderStrength: 0f,
            settlementAdvantage: 2f,
            siegeElapsedHours: 0f,
            hasRam: false,
            hasSiegeTower: false,
            equipmentBuilt: 0,
            maximumEquipmentProgress: 0f));

        Assert.True(result.IsViable);
    }

    [Fact]
    public void EmptyAttackAndDefense_IsNotViable()
    {
        var result = readiness.Evaluate(new AiSiegeAssaultReadinessInput(
            attackerStrength: 0f,
            defenderStrength: 0f,
            settlementAdvantage: 1f,
            siegeElapsedHours: 96f,
            hasRam: true,
            hasSiegeTower: true,
            equipmentBuilt: 3,
            maximumEquipmentProgress: 0f));

        Assert.True(float.IsNaN(result.PowerRatioBeforeEquipment));
        Assert.False(result.IsViable);
    }

    private static AiSiegeAssaultReadinessInput CreateTimedInput(float siegeElapsedHours)
    {
        return new AiSiegeAssaultReadinessInput(
            attackerStrength: 150f,
            defenderStrength: 100f,
            settlementAdvantage: 2f,
            siegeElapsedHours,
            hasRam: true,
            hasSiegeTower: true,
            equipmentBuilt: 3,
            maximumEquipmentProgress: 0f);
    }
}
