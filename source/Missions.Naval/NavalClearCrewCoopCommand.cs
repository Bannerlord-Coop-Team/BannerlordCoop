#if DEBUG
using Common.Commands;
using GameInterface.Services.MapEvents.Commands;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.clear_crew 1d93
/// <summary>
/// DEBUG: on the client that simulates a hull, kills its formation's units and empties its reserve through vanilla, so the
/// deaths replicate as usual and the hull reads as vacant for a capture.
/// </summary>
public sealed class NavalClearCrewCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.clear_crew <shipId-prefix>";

    public string Prefix => "coop.debug.naval";

    public string Name => "clear_crew";

    public string Description => "On the hull's authority: kills the hull's formation's units (the main agent excepted) and empties its " +
        "reserve, so the hull is vacant for a capture.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "The start of the hull's ship id, as coop.debug.naval.inspect lists it.", true),
    };

    // Kills the crew and empties the reserve of the one hull this client simulates whose ship id starts with the prefix.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 1) return Failed(Usage);

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var agentsLogic = mission?.GetMissionBehavior<NavalAgentsLogic>();
        if (controller == null || agentsLogic == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");

        var registry = controller.MissionComponent.ShipRegistry;
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), args[0], out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull) return Failed("The hull is not a naval ship.");
        if (info.CurrentAuthority != controller.Session.OwnControllerId)
            return Failed("Hull " + shipId + " is simulated by " + info.CurrentAuthority + "; run clear_crew there.");

        return Clear(mission, agentsLogic, hull, shipId);
    }

    // Vanilla's vacancy reads the formation's units and the hull's queued reserve, so both go.
    private static CoopCommandResult Clear(Mission mission, NavalAgentsLogic agentsLogic, MissionShip hull, Guid shipId)
    {
        var formation = hull.Formation;
        var crew = formation == null ? Array.Empty<Agent>()
            : mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman && agent.Formation == formation).ToArray();
        int killed = 0;
        foreach (var agent in crew.Where(agent => !agent.IsMainAgent))
        {
            BattleTeamKillCommands.Kill(agent);
            killed++;
        }

        int reserve = agentsLogic.GetReservedTroopsCountOfShip(hull);
        if (hull.Team != null && reserve > 0) agentsLogic.RemoveAllReservedTroopsFromShip(hull);

        bool mainKept = crew.Any(agent => agent.IsMainAgent);
        return new CoopCommandResult(true, "NAVAL_CLEAR_CREW ship=" + shipId + " killed=" + killed + " reserveRemoved=" + reserve +
            (mainKept ? " mainAgentInFormation=true (vacancy also needs it gone)" : "") +
            "; vanilla's vacancy query refreshes within 5 s");
    }

    // A failed command result with output.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
