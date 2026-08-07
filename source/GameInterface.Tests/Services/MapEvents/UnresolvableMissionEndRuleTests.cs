using GameInterface.Services.MapEvents.Handlers;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// When a client may end the battle mission it is sitting in, on being told the battle was finalized
/// (issue #1840).
/// </summary>
/// <remarks>
/// The close instruction covers two very different endings. A battle that reached a VICTORY still owns its
/// screens - the winner reads the scoreboard and leaves when it chooses - and cutting that off would skip the
/// result pass. A battle finalized with nothing decided, because the only opposing player walked out of it, is
/// the opposite: its map event is gone, no end condition can ever fire, and a defender still in the deployment
/// phase cannot reach one at all, since vanilla's BattleEndLogic skips every check while a
/// BattleDeploymentHandler is on the mission. That mission has to be ended here or the player is stranded.
/// </remarks>
public class UnresolvableMissionEndRuleTests
{
    [Fact]
    public void AMissionWithNoResultWhoseBattleIsGone_IsEnded()
    {
        Assert.True(PvPInteractionClientHandler.ShouldEndUnresolvableMission(
            hasMission: true,
            hasMissionResult: false,
            missionEnded: false,
            namesABattle: true,
            battleStillExists: false));
    }

    [Fact]
    public void AWinnersMission_IsLeftAlone()
    {
        // The whole point of the guard this rule sits behind: the winner's scoreboard must survive the finalize.
        Assert.False(PvPInteractionClientHandler.ShouldEndUnresolvableMission(
            hasMission: true,
            hasMissionResult: true,
            missionEnded: false,
            namesABattle: true,
            battleStillExists: false));
    }

    [Fact]
    public void ABattleThisClientCanStillResolve_IsLeftAlone()
    {
        // The map event is still there, so the fight is not over - this client is just being told to close a
        // menu, and ending its mission would abandon a battle still being fought.
        Assert.False(PvPInteractionClientHandler.ShouldEndUnresolvableMission(
            hasMission: true,
            hasMissionResult: false,
            missionEnded: false,
            namesABattle: true,
            battleStillExists: true));
    }

    [Fact]
    public void ACloseThatNamesNoBattle_IsNotEvidenceTheFightEnded()
    {
        Assert.False(PvPInteractionClientHandler.ShouldEndUnresolvableMission(
            hasMission: true,
            hasMissionResult: false,
            missionEnded: false,
            namesABattle: false,
            battleStillExists: false));
    }

    [Theory]
    [InlineData(false, false)] // no mission at all
    [InlineData(true, true)]   // the mission is already on its way out
    public void NothingToEnd_IsNotEnded(bool hasMission, bool missionEnded)
    {
        Assert.False(PvPInteractionClientHandler.ShouldEndUnresolvableMission(
            hasMission,
            hasMissionResult: false,
            missionEnded: missionEnded,
            namesABattle: true,
            battleStillExists: false));
    }
}
