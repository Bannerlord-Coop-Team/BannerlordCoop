#if DEBUG
using Common.Commands;
using Missions.Battles;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.reswap 1d93
/// <summary>
/// DEBUG: replaces an AI hull this client simulates with a fresh simulated hull through the migration's swap path, so
/// the swap can be tried without a host migration.
/// </summary>
public sealed class NavalReswapCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";

    public string Name => "reswap";

    public string Description => "Replaces an AI hull this client simulates with a fresh one, as a battle-host migration does.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "The start of the AI hull's ship id, as coop.debug.naval.inspect lists it.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var naval = mission?.GetMissionBehavior<CoopNavalBattleBehavior>();
        if (controller == null || naval == null) return Failed("No active coop naval battle mission.");
        if (args == null || args.Count != 1) return Failed("Usage: coop.debug.naval.reswap <shipId-prefix>");

        var error = TryResolveShipId(controller.MissionComponent.ShipRegistry.Ships.Select(ship => ship.ShipId), args[0], out var shipId)
            ?? naval.ShipReplicator.ReplaceNpcHull(shipId);
        if (error != null) return Failed(error);

        return new CoopCommandResult(true, "NAVAL_RESWAP ship=" + shipId + " queued: phases ParkAgents, ReplaceHull, BoardAgents " +
            "run on the next three mission ticks ([NavalSync] Hull swap log lines, shipSync.ships[].swapPhase in coop.debug.naval.inspect)");
    }

    /// <summary>The one ship id that starts with <paramref name="prefix"/>; why there is none, or null.</summary>
    internal static string TryResolveShipId(IEnumerable<Guid> shipIds, string prefix, out Guid shipId)
    {
        shipId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(prefix)) return "shipId-prefix must not be empty.";

        var matches = shipIds.Where(id => id.ToString("D").StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0) return "No registered hull's ship id starts with " + prefix + ".";
        if (matches.Length > 1) return "Several registered hulls' ship ids start with " + prefix + "; give more of the id.";

        shipId = matches[0];
        return null;
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
