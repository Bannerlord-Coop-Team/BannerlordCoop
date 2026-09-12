using GameInterface.Services.MapEvents;
using TaleWorlds.Core;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Pure capacity math for the BR-110 engine agent budget. The spawn-path behavior (reinforcement parties,
/// puppets, supplier waves honoring the budget) lives in <see cref="BattleAgentRenderCapTests"/>.
/// </summary>
public class BattleAgentBudgetTests
{
    private readonly IBattleAgentBudget budget = new BattleAgentBudget();

    /// <summary>Equipment whose Horse slot carries a real mount (a HorseComponent item) — the engine spawns a
    /// rider AND its mount from this in one SpawnAgent call.</summary>
    internal static Equipment MountedEquipment()
    {
        var horse = new ItemObject { ItemComponent = new HorseComponent() };
        var equipment = new Equipment();
        equipment[EquipmentIndex.Horse] = new EquipmentElement(horse);
        return equipment;
    }

    [Fact]
    [Trait("Requirement", "BR-110")]
    public void EngineLimit_IsTwoThousandAgents()
    {
        Assert.Equal(2000, budget.MaxRenderedAgents);
    }

    [Theory]
    [Trait("Requirement", "BR-110")]
    [InlineData(0, 2000)]
    [InlineData(1, 1999)]
    [InlineData(1999, 1)]
    [InlineData(2000, 0)]
    [InlineData(2500, 0)] // already past the limit — never negative
    public void RemainingCapacity_IsLimitMinusLiveAndNeverNegative(int liveAgents, int expected)
    {
        Assert.Equal(expected, budget.RemainingCapacity(liveAgents));
    }

    [Fact]
    [Trait("Requirement", "BR-110")]
    public void NullMission_HasCapacityAndDoesNotClamp()
    {
        // No mission means no engine to overload — suppliers driven outside a mission stay unclamped.
        Assert.True(budget.HasCapacityFor(null, 5000));
        Assert.Equal(300, budget.ClampToCapacity(null, 300));
        Assert.Equal(0, budget.CountLiveAgents(null));
    }

    [Fact]
    [Trait("Requirement", "BR-110")]
    public void SlotsForEquipment_CountsAMountAsTwoSlots()
    {
        // A mounted troop spawns a rider AND a horse in one SpawnAgent call — two render slots.
        Assert.Equal(2, budget.SlotsForEquipment(MountedEquipment()));
    }

    [Fact]
    [Trait("Requirement", "BR-110")]
    public void SlotsForEquipment_CountsUnmountedAndEmptyAsOneSlot()
    {
        Assert.Equal(1, budget.SlotsForEquipment(new Equipment())); // empty Horse slot
        Assert.Equal(1, budget.SlotsForEquipment(null));            // no equipment at all

        // A Horse-slot item WITHOUT a horse component is not a ridable mount — still one slot.
        var equipment = new Equipment();
        equipment[EquipmentIndex.Horse] = new EquipmentElement(new ItemObject());
        Assert.Equal(1, budget.SlotsForEquipment(equipment));
    }

    [Fact]
    [Trait("Requirement", "BR-110")]
    public void SlotsForOrigin_NullOrigin_SpawnsNothingAndCostsNothing()
    {
        // (Mounted/unmounted origin costing is covered through the drip-clamp tests in
        // BattleAgentRenderCapTests, which need the harness's registered CharacterObjects.)
        Assert.Equal(0, budget.SlotsForOrigin(null));
    }
}
