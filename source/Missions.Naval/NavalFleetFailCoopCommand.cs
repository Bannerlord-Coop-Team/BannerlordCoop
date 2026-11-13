#if DEBUG
using Common.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Arms the battle host's enemy-side AI fleet attempt to fail through its normal exception path.</summary>
public sealed class NavalFleetFailCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";
    public string Name => "fleet_fail";
    public string Description =>
        "Fails the next AI fleet spawn attempt in this battle at a stage (before, after_hull, crewing; default before); run on the battle host before Ready.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    private const string Usage = "Usage: coop.debug.naval.fleet_fail [before|after_hull|crewing]";

    // The stage word each failure point answers to.
    private static readonly Dictionary<string, NavalFleetFailStage> Stages = new Dictionary<string, NavalFleetFailStage>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["before"] = NavalFleetFailStage.BeforeSpawn,
        ["after_hull"] = NavalFleetFailStage.AfterFirstHull,
        ["crewing"] = NavalFleetFailStage.DuringCrewing,
    };

    // Commands already run on the game thread, where the mission's spawner owns its one-shot flag.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var stage = NavalFleetFailStage.BeforeSpawn;
        if (args.Count > 1 || (args.Count == 1 && !Stages.TryGetValue((args[0] ?? "").Trim(), out stage)))
            return new CoopCommandResult(false, Usage, "command_failed");

        var mission = Mission.Current;
        var naval = mission?.GetMissionBehavior<CoopNavalBattleBehavior>();
        if (naval == null || mission.PlayerTeam == null)
            return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");

        // The enemy side's fleet; every battle has a spawner for each side.
        var enemySide = mission.PlayerTeam.Side.GetOppositeSide();
        var spawner = naval.NpcFleetSpawners.First(npcFleet => npcFleet.Side == enemySide);
        var error = spawner.FailNextSpawn(stage);
        if (error != null) return new CoopCommandResult(false, error, "command_failed");

        return new CoopCommandResult(true,
            $"NAVAL_FLEET_FAIL armed ({stage}): next eligible {enemySide} AI fleet spawn attempt will fail; inspect " +
            $"npcFleet.spawners.{enemySide}.state, .failure and .hullsRetracted.");
    }
}
#endif
