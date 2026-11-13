#if DEBUG
using Common.Commands;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Arms the battle host's enemy or allied AI fleet attempt to fail through its normal exception path.</summary>
public sealed class NavalFleetFailCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";
    public string Name => "fleet_fail";
    public string Description =>
        "Fails the next AI fleet spawn attempt of the enemy (default) or the allied side at a stage (before, after_hull, crewing; " +
        "default before); run on the battle host before Ready.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    private const string Usage = "Usage: coop.debug.naval.fleet_fail [enemy|ally] [before|after_hull|crewing]";

    // The stage word each failure point answers to.
    private static readonly Dictionary<string, NavalFleetFailStage> Stages = new Dictionary<string, NavalFleetFailStage>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["before"] = NavalFleetFailStage.BeforeSpawn,
        ["after_hull"] = NavalFleetFailStage.AfterFirstHull,
        ["crewing"] = NavalFleetFailStage.DuringCrewing,
    };

    // Whether each side word names the host's own side.
    private static readonly Dictionary<string, bool> Sides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
    {
        ["enemy"] = false,
        ["ally"] = true,
    };

    // Commands already run on the game thread, where the mission's spawner owns its one-shot flag.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out bool ally, out var stage)) return new CoopCommandResult(false, Usage, "command_failed");

        var mission = Mission.Current;
        var naval = mission?.GetMissionBehavior<CoopNavalBattleBehavior>();
        if (naval == null || mission.PlayerTeam == null)
            return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");

        // Allies are the AI parties on this client's own side; every battle has a spawner for each side.
        var side = ally ? mission.PlayerTeam.Side : mission.PlayerTeam.Side.GetOppositeSide();
        var error = naval.NpcFleetSpawners[side].FailNextSpawn(stage);
        if (error != null) return new CoopCommandResult(false, error, "command_failed");

        return new CoopCommandResult(true,
            $"NAVAL_FLEET_FAIL armed ({stage}): next eligible {side} AI fleet spawn attempt will fail; inspect " +
            $"npcFleet.spawners.{side}.state, .failure and .hullsRetracted.");
    }

    // Reads up to one side word (enemy by default) and one stage word (before by default), in either order.
    private static bool TryParse(ICoopCommandArgs args, out bool ally, out NavalFleetFailStage stage)
    {
        ally = false;
        stage = NavalFleetFailStage.BeforeSpawn;
        int sideWords = 0, stageWords = 0;
        for (int index = 0; index < args.Count; index++)
        {
            var word = (args[index] ?? "").Trim();
            if (Stages.TryGetValue(word, out var stageWord))
            {
                stage = stageWord;
                stageWords++;
                continue;
            }

            if (!Sides.TryGetValue(word, out var allyWord)) return false;
            ally = allyWord;
            sideWords++;
        }

        return sideWords <= 1 && stageWords <= 1;
    }
}
#endif
