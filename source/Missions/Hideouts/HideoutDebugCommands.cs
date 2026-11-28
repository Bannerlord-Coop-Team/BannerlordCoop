using Common.Commands;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

public sealed class HideoutTeleportBossCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.hideout";
    public string Name => "teleport_boss";
    public string Description => "Teleports every player's hero in the current hideout mission to the spawned boss.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var hideout = Mission.Current?.GetMissionBehavior<CoopHideoutMissionLogic>();
        if (hideout == null)
            return new CoopCommandResult(false, "Not in a co-op hideout mission.", "mission_unavailable");
        if (!hideout.RequestTeleportToBoss())
            return new CoopCommandResult(false, "The boss has not spawned yet.", "boss_unavailable");
        return new CoopCommandResult(true, "Teleported every player to the hideout boss.");
    }
}
