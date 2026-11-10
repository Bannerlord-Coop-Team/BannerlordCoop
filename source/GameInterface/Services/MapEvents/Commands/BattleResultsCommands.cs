#if DEBUG
using Common.Commands;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace GameInterface.Services.MapEvents.Commands;

// coop.debug.map_event.scoreboard
/// <summary>DEBUG: opens the ended battle's results screen, as Tab does once the battle is over. Land and naval.</summary>
public sealed class BattleResultsScoreboardCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.map_event";

    public string Name => "scoreboard";

    public string Description => "Opens the battle results screen once the battle is over, as Tab does.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryGetResults(out var mission, out var scoreboard, out var failure)) return Failed(failure);
        if (scoreboard.DataSource.ShowScoreboard) return Opened("MAP_EVENT_SCOREBOARD already open", scoreboard);

        // Same as MissionGauntletBattleScore's Tab press: silence the end logic's reminders, then open.
        (mission.MissionBehaviors.FirstOrDefault(behavior => behavior is IBattleEndLogic) as IBattleEndLogic)?.SetNotificationDisabled(true);
        scoreboard.OnOpen();

        if (!scoreboard.DataSource.ShowScoreboard) return Failed("The results screen did not open (mission mode " + mission.Mode + ").");
        return Opened("MAP_EVENT_SCOREBOARD opened", scoreboard);
    }

    /// <summary>[Game thread] The ended battle mission and its battle scoreboard view, once its results are shown.</summary>
    internal static bool TryGetResults(out Mission mission, out MissionGauntletBattleScore scoreboard, out string failure)
    {
        mission = Mission.Current;
        scoreboard = null;
        failure = null;

        if (mission == null)
        {
            failure = "No active battle mission.";
            return false;
        }

        scoreboard = mission.GetMissionBehavior<MissionGauntletBattleScore>();
        if (scoreboard?.DataSource == null)
        {
            failure = "No battle scoreboard UI.";
            return false;
        }

        if (!mission.MissionEnded)
        {
            failure = "The battle has not ended on this client.";
            return false;
        }

        // SPScoreboardVM marks the results ready 1.5 s after the mission starts ending.
        if (!scoreboard.DataSource.IsOver)
        {
            failure = "The results are not ready yet; retry in a moment.";
            return false;
        }

        return true;
    }

    internal static CoopCommandResult Opened(string label, MissionGauntletBattleScore scoreboard) =>
        new CoopCommandResult(true, label + " result=" + scoreboard.DataSource.BattleResult);

    internal static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}

// coop.debug.map_event.leave_results
/// <summary>DEBUG: presses Done on the open results screen, so the mission ends exactly as a click ends it. Land and naval.</summary>
public sealed class BattleResultsLeaveCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.map_event";

    public string Name => "leave_results";

    public string Description => "Presses Done on the open battle results screen and leaves the mission.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!BattleResultsScoreboardCoopCommand.TryGetResults(out var mission, out var scoreboard, out var failure))
            return BattleResultsScoreboardCoopCommand.Failed(failure);
        if (!scoreboard.DataSource.ShowScoreboard)
            return BattleResultsScoreboardCoopCommand.Failed("The results screen is not open; run coop.debug.map_event.scoreboard first.");

        // The Done button's action: SPScoreboardVM asks the land BattleEndLogic to exit, or ends the mission without one.
        scoreboard.DataSource.ExecuteQuitAction();
        return new CoopCommandResult(true, "MAP_EVENT_LEAVE_RESULTS pressed Done; mission state=" + mission.CurrentState);
    }
}
#endif
