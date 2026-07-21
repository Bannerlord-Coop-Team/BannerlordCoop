using GameInterface.Services.MapEvents.TroopSupply;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

public class BattleInitialSpawnAllocatorTests
{
    private readonly BattleInitialSpawnAllocator allocator = new BattleInitialSpawnAllocator();

    [Fact]
    public void Allocate_ClampsLargerSideToSeventyFivePercent()
    {
        var result = Allocate(100,
            Party("defender", "D", BattleSideEnum.Defender, 100),
            Party("attacker", "A", BattleSideEnum.Attacker, 900));

        Assert.Equal(25, result["defender"]);
        Assert.Equal(75, result["attacker"]);
    }

    [Fact]
    public void Allocate_EqualSidesReceiveFiveHundredSlotsEach()
    {
        var result = Allocate(1000,
            Party("defender", "D", BattleSideEnum.Defender, 800),
            Party("attacker", "A", BattleSideEnum.Attacker, 800));

        Assert.Equal(500, result["defender"]);
        Assert.Equal(500, result["attacker"]);
    }

    [Fact]
    public void Allocate_SpillsUnusedShortSideSlotsToLongSide()
    {
        var result = Allocate(100,
            Party("defender", "D", BattleSideEnum.Defender, 10),
            Party("attacker", "A", BattleSideEnum.Attacker, 990));

        Assert.Equal(10, result["defender"]);
        Assert.Equal(90, result["attacker"]);
    }

    [Fact]
    public void Allocate_WhenCapacityIsBelowBudget_AllocatesEveryTroop()
    {
        var result = Allocate(1000,
            Party("defender", "D", BattleSideEnum.Defender, 25),
            Party("attacker", "A", BattleSideEnum.Attacker, 40));

        Assert.Equal(25, result["defender"]);
        Assert.Equal(40, result["attacker"]);
    }

    [Fact]
    public void Allocate_DividesSideBetweenAuthoritiesBeforeTheirParties()
    {
        var result = Allocate(100,
            Party("A-large", "A", BattleSideEnum.Attacker, 90),
            Party("A-small", "A", BattleSideEnum.Attacker, 10),
            Party("B-only", "B", BattleSideEnum.Attacker, 100));

        Assert.Equal(45, result["A-large"]);
        Assert.Equal(5, result["A-small"]);
        Assert.Equal(50, result["B-only"]);
    }

    [Fact]
    public void Allocate_GivesEachPartyOneSlotWhenBudgetPermits()
    {
        var result = Allocate(2,
            Party("large", "A", BattleSideEnum.Attacker, 99),
            Party("small", "A", BattleSideEnum.Attacker, 1));

        Assert.Equal(1, result["large"]);
        Assert.Equal(1, result["small"]);
    }

    [Fact]
    public void Allocate_WhenAuthorityHasOneSlot_PrioritizesItsDirectPlayerParty()
    {
        var result = Allocate(1,
            Party("ai-army-party", "player", BattleSideEnum.Attacker, 100),
            Party("player-party", "player", BattleSideEnum.Attacker, 1, isDirectPlayerParty: true));

        Assert.Equal(0, result["ai-army-party"]);
        Assert.Equal(1, result["player-party"]);
    }

    [Fact]
    public void Allocate_LargestRemainderTieUsesPartyIdRegardlessOfInputOrder()
    {
        var first = Allocate(3,
            Party("party-B", "A", BattleSideEnum.Attacker, 10),
            Party("party-A", "A", BattleSideEnum.Attacker, 10));
        var second = Allocate(3,
            Party("party-A", "A", BattleSideEnum.Attacker, 10),
            Party("party-B", "A", BattleSideEnum.Attacker, 10));

        Assert.Equal(2, first["party-A"]);
        Assert.Equal(1, first["party-B"]);
        Assert.Equal(first["party-A"], second["party-A"]);
        Assert.Equal(first["party-B"], second["party-B"]);
    }

    [Fact]
    public void Allocate_NeverExceedsGlobalBudgetOrPartyCapacity()
    {
        var capacities = new Dictionary<string, int>
        {
            ["defender-player"] = 3,
            ["defender-ai"] = 80,
            ["attacker-a"] = 17,
            ["attacker-b"] = 200,
        };
        var result = Allocate(37,
            Party("defender-player", "D", BattleSideEnum.Defender, capacities["defender-player"], isDirectPlayerParty: true),
            Party("defender-ai", "D", BattleSideEnum.Defender, capacities["defender-ai"]),
            Party("attacker-a", "A", BattleSideEnum.Attacker, capacities["attacker-a"]),
            Party("attacker-b", "B", BattleSideEnum.Attacker, capacities["attacker-b"]));

        Assert.True(result.Values.Sum() <= 37);
        foreach (var allocation in result)
            Assert.InRange(allocation.Value, 0, capacities[allocation.Key]);
    }

    private IReadOnlyDictionary<string, int> Allocate(int battleSize, params BattleInitialSpawnParty[] parties)
        => allocator.Allocate(battleSize, parties);

    private static BattleInitialSpawnParty Party(
        string partyId,
        string authorityId,
        BattleSideEnum side,
        int capacity,
        bool isDirectPlayerParty = false)
        => new BattleInitialSpawnParty(partyId, authorityId, side, capacity, isDirectPlayerParty);
}
