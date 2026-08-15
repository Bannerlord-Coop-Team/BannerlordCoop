using Common.Util;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Interfaces;

public class AwaitingAlternativeSolutionTroopsRegistryTests
{
    private static CharacterObject NewCharacter() => ObjectHelper.SkipConstructor<CharacterObject>();

    private static TroopRoster Roster(params (CharacterObject Character, int Number)[] elements)
    {
        var roster = new TroopRoster();
        foreach (var (character, number) in elements)
        {
            roster.AddToCounts(character, number, false, 0, 0, true);
        }
        return roster;
    }

    [Fact]
    public void Withdraw_RemovesOnlyTheReportedTroops_LeavesUnrelatedDepositsIntact()
    {
        var registry = new AwaitingAlternativeSolutionTroopsRegistry();
        var x = NewCharacter();
        var y = NewCharacter();
        var z = NewCharacter();

        registry.Deposit("player-A", Roster((x, 2), (y, 3)));
        registry.Deposit("player-A", Roster((z, 2)));

        registry.Withdraw("player-A", Roster((x, 2), (y, 3)));

        Assert.True(registry.TryGet("player-A", out var remaining));
        Assert.Equal(2, remaining.GetTroopCount(z));
        Assert.Equal(0, remaining.GetTroopCount(x));
        Assert.Equal(0, remaining.GetTroopCount(y));
    }

    [Fact]
    public void Withdraw_ExactlyTheWholeBalance_RemovesTheEntryEntirely()
    {
        var registry = new AwaitingAlternativeSolutionTroopsRegistry();
        var x = NewCharacter();

        registry.Deposit("player-A", Roster((x, 5)));
        registry.Withdraw("player-A", Roster((x, 5)));

        Assert.False(registry.TryGet("player-A", out _));
    }

    [Fact]
    public void Withdraw_ClaimMoreThanIsActuallyPresent_ClampsInsteadOfGoingNegative()
    {
        var registry = new AwaitingAlternativeSolutionTroopsRegistry();
        var x = NewCharacter();

        registry.Deposit("player-A", Roster((x, 3)));
        registry.Withdraw("player-A", Roster((x, 10)));

        Assert.False(registry.TryGet("player-A", out _));
    }

    [Fact]
    public void Withdraw_UnknownOwner_NoOp()
    {
        var registry = new AwaitingAlternativeSolutionTroopsRegistry();
        var x = NewCharacter();

        registry.Withdraw("player-A", Roster((x, 1)));

        Assert.False(registry.TryGet("player-A", out _));
    }

    [Fact]
    public void Withdraw_CalledWithTheSameLiveRosterReturnedByTryGet_DoesNotCorruptFromAliasing()
    {
        var registry = new AwaitingAlternativeSolutionTroopsRegistry();
        var x = NewCharacter();
        var y = NewCharacter();

        registry.Deposit("player-A", Roster((x, 2), (y, 3)));

        Assert.True(registry.TryGet("player-A", out var stored));
        registry.Withdraw("player-A", stored);

        Assert.False(registry.TryGet("player-A", out _));
    }
}
