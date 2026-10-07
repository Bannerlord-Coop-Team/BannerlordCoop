#if DEBUG
using Common.Commands;
using Missions.Battles;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.win
/// <summary>DEBUG, battle host: ends the coop naval battle as a victory for the host's side through the native end check.</summary>
public sealed class NavalWinCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";

    public string Name => "win";

    public string Description => "On the battle host, ends the naval battle as a victory for the host's side; peers follow the host's result.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => Force(playerVictory: true, "NAVAL_WIN");

    /// <summary>[Game thread] Latches the outcome on the host's end logic; the mission's next end check reports it.</summary>
    internal static CoopCommandResult Force(bool playerVictory, string label)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var endLogic = mission?.GetMissionBehavior<CoopNavalBattleEndLogic>();
        if (controller == null || endLogic == null) return Failed("No active coop naval battle mission.");
        if (!controller.Session.IsLocalHost)
            return Failed("Only the battle host decides the naval battle end; run this on the client whose coop.debug.naval.inspect shows isHost true.");
        if (!mission.IsDeploymentFinished) return Failed("Deployment has not finished on this client.");
        if (mission.MissionEnded) return Failed("The mission has already ended with " + CoopNavalBattleEndLogic.DescribeResult(mission.MissionResult) + ".");

        endLogic.ForceOutcome(playerVictory);
        return new CoopCommandResult(true, label + " side=" + mission.PlayerTeam?.Side + " outcome=" + (playerVictory ? "victory" : "defeat") +
            "; the host's next end check concludes it and the result snapshot carries it to the peers");
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}

// coop.debug.naval.lose
/// <summary>DEBUG, battle host: ends the coop naval battle as a defeat for the host's side through the native end check.</summary>
public sealed class NavalLoseCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";

    public string Name => "lose";

    public string Description => "On the battle host, ends the naval battle as a defeat for the host's side; peers follow the host's result.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => NavalWinCoopCommand.Force(playerVictory: false, "NAVAL_LOSE");
}
#endif
