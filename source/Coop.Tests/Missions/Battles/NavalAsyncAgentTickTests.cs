#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R48 re-review: work outside the mission tick reads whether the async agent tick runs, without waiting.</summary>
public class NavalAsyncAgentTickTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void IsRunning_OnlyOutsideTheMissionTickWithTheTickPending(bool inMissionTick, bool tickCompleted, bool expected)
    {
        Assert.Equal(expected, NavalAsyncAgentTick.IsTickRunning(inMissionTick, tickCompleted));
    }

    // Without a mission there is no async agent tick.
    [Fact]
    public void IsRunning_WithoutAMission_IsFalse()
    {
        Assert.False(new NavalAsyncAgentTick().IsRunning);
    }

    // A formation noted twice is checked once; one without a formation AI is left alone, and nothing outlives the battle.
    [Fact]
    public void TacticRefresh_NotesEachFormationOnce_AndSkipsOneWithoutAi()
    {
        var refresh = new NavalFormationTacticRefresh();
        var formation = (TaleWorlds.MountAndBlade.Formation)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(TaleWorlds.MountAndBlade.Formation));

        refresh.Note(null!);
        refresh.Note(formation);
        refresh.Note(formation);
        Assert.Equal(1, refresh.Noted);
        refresh.Apply();
        Assert.Equal(0, refresh.Noted);
        refresh.Clear();
    }
}
#endif
