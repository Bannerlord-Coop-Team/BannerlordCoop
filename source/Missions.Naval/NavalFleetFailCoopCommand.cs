#if DEBUG
using Common.Commands;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Arms the current battle host's one AI fleet attempt to fail through its normal exception path.</summary>
public sealed class NavalFleetFailCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";
    public string Name => "fleet_fail";
    public string Description => "Fails the next AI fleet spawn attempt in this battle; run on the battle host before Ready.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    // Commands already run on the game thread, where the mission's spawner owns its one-shot flag.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count != 0)
            return new CoopCommandResult(false, "Usage: coop.debug.naval.fleet_fail", "command_failed");

        var spawner = Mission.Current?.GetMissionBehavior<CoopNavalBattleBehavior>()?.NpcFleetSpawner;
        if (spawner == null)
            return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");

        var error = spawner.FailNextSpawn();
        if (error != null) return new CoopCommandResult(false, error, "command_failed");

        return new CoopCommandResult(true,
            "NAVAL_FLEET_FAIL armed: next eligible AI fleet spawn attempt will fail; inspect npcFleet.spawner.state and npcFleet.spawner.failure.");
    }
}
#endif
