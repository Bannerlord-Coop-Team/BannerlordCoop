#if DEBUG
using Common.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("side", "enemy (default) or ally, the host's own side.", false),
        new ExpectedArgs("stage", "before (default), after_hull or crewing.", false),
    };

    private const string Usage = "Usage: coop.debug.naval.fleet_fail [enemy|ally] [before|after_hull|crewing]";

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

    /// <summary>
    /// Reads the usage by position: an optional side word (enemy by default), then an optional stage word (before by default),
    /// and nothing after them.
    /// </summary>
    internal static bool TryParse(ICoopCommandArgs args, out bool ally, out NavalFleetFailStage stage)
    {
        ally = false;
        stage = NavalFleetFailStage.BeforeSpawn;
        var words = Enumerable.Range(0, args.Count).Select(index => (args[index] ?? "").Trim()).ToList();
        int next = 0;
        if (next < words.Count && IsSideWord(words[next], out ally)) next++;
        if (next == words.Count) return true;

        if (!Stages.TryGetValue(words[next], out stage)) return false;
        return next + 1 == words.Count;
    }

    // Whether the word names a side, and whether that side is the host's own.
    private static bool IsSideWord(string word, out bool ally)
    {
        ally = word.Equals("ally", StringComparison.OrdinalIgnoreCase);
        return ally || word.Equals("enemy", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
