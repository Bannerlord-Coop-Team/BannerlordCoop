using Common.Commands;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Read-only: the rope at every throw station of every registered hull on this client.</summary>
public sealed class NavalRopeStatusCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";

    public string Name => "rope_status";

    public string Description => "Reports every hull's rope and plank state and the rope force counters on this client.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var naval = Mission.Current?.GetMissionBehavior<CoopNavalBattleBehavior>();
        if (naval == null)
            return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");

        return new CoopCommandResult(true, "NAVAL_ROPES " + JsonConvert.SerializeObject(naval.ShipReplicator.InspectRopes()));
    }
}
