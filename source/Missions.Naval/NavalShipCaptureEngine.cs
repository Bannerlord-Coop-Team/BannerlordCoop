using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.AI.TeamAI;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <inheritdoc cref="INavalShipCaptureEngine"/>
public class NavalShipCaptureEngine : INavalShipCaptureEngine
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalShipCaptureEngine>();

    private static NavalShipsLogic ShipsLogic => Mission.Current?.GetMissionBehavior<NavalShipsLogic>();

    // Refuses before touching anything when a precondition fails; a throw inside vanilla's swap is logged (RunVanillaSwap).
    public void ApplyCapture(MissionObject prize, MissionObject captorHull)
    {
        var blocker = CheckCapture(prize, captorHull);
        if (blocker != null)
        {
            Logger.Warning("[NavalSync] Refused replaying the capture of hull {Index} here: {Reason}", (prize as MissionShip)?.Index, blocker);
            return;
        }

        var ship = (MissionShip)prize;
        var captor = (MissionShip)captorHull;
        RunVanillaSwap(ship, captor);
        // Vanilla's swap hands both hulls an auto-updated controller; a copy keeps none, so it never steers itself here.
        if (NavalForeignHulls.Contains(ship)) ship.SetController(ShipControllerType.None, autoUpdateController: false);
        if (NavalForeignHulls.Contains(captor)) captor.SetController(ShipControllerType.None, autoUpdateController: false);
        // As the helm's capture branch does once the swap ran.
        ship.InvalidateActiveFormationTroopOnShipCache();
        captor.InvalidateActiveFormationTroopOnShipCache();
    }

    // Diagnostic only: vanilla's swap (NavalShipsLogic.OnShipCaptured) moves assignments, formations and controllers, then runs
    // the ship orders, the query expiry, ShipCapturedEvent's agent rebinding and the team AI refresh, so a throw half-applies it.
    private static void RunVanillaSwap(MissionShip ship, MissionShip captor)
    {
        try
        {
            ShipsLogic.OnShipCaptured(ship, captor.Formation);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "[NavalSync] Vanilla's capture swap of hull {Index} into hull {CaptorIndex}'s formation is half-applied: " +
                "it threw in its {Stage} after the checks passed", ship.Index, captor.Index, SwapStage(exception.StackTrace));
        }
    }

    /// <summary>Which step of vanilla's swap a throw came from, read from its stack (the steps run one after another).</summary>
    internal static string SwapStage(string stackTrace)
    {
        if (stackTrace == null) return "unknown step";
        if (stackTrace.Contains("RefreshTeamAIBehaviorShipReferences")) return "team AI refresh";
        if (stackTrace.Contains("NavalAgentsLogic.OnShipCaptured")) return "agent rebinding (ShipCapturedEvent)";
        if (stackTrace.Contains("ForceExpireAll")) return "query expiry";
        if (stackTrace.Contains("ShipOrder.OnShipCaptured")) return "ship orders";
        return "assignment and formation swap";
    }

    /// <summary>Why vanilla's swap can't run on the two hulls here, or null: everything NavalShipsLogic.OnShipCaptured dereferences.</summary>
    public string CheckCapture(MissionObject prize, MissionObject captorHull)
    {
        if (prize is not MissionShip ship || captorHull is not MissionShip captor) return "not_naval_hulls";
        if (ship == captor) return "same_hull";

        var shipsLogic = ShipsLogic;
        if (shipsLogic == null) return "no_naval_mission";
        if (HasLeftMission(ship) || HasLeftMission(captor)) return "hull_removed";

        var formation = captor.Formation;
        if (formation?.Team == null) return "captor_hull_has_no_formation";
        if (formation.Team.TeamAI is not TeamAINavalComponent) return "captor_team_has_no_naval_ai";
        if (shipsLogic.GetShipAssignment(formation.Team.TeamSide, formation.FormationIndex)?.MissionShip != captor)
            return "captor_formation_holds_another_hull";

        var prizeFormation = ship.Formation;
        if (prizeFormation != null && prizeFormation.Team == null) return "prize_formation_has_no_team";
        if (prizeFormation != null && prizeFormation.Team.TeamAI != null && prizeFormation.Team.TeamAI is not TeamAINavalComponent)
            return "prize_team_has_no_naval_ai";

        return null;
    }

    // A hull that left the mission or lost its entity.
    private static bool HasLeftMission(MissionShip ship) => ship.IsRemoved || !ship.GameEntity.IsValid;

    // Vanilla's swap gives the captor's assignment to the prize, so the orphan holds none; a joiner's copy had one of its own.
    public void OrphanHull(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        var shipsLogic = ShipsLogic;
        if (shipsLogic != null && shipsLogic.FindAssignmentOfShipOrigin(ship.ShipOrigin, out var assignment))
        {
            assignment.RemoveShip();
            assignment.Clear();
        }

        ship.SetFormation(null);
        ship.SetController(ShipControllerType.None, autoUpdateController: false);
    }

    // The formation part of vanilla's vacancy is a query cached for 5 s, so the authority refreshes it before deciding.
    public bool IsVacantForCapture(MissionObject hull)
    {
        var ship = (MissionShip)hull;
        ship.InvalidateActiveFormationTroopOnShipCache();
        return ship.ShipControllerMachine?.IsAttachedShipVacant() == true;
    }

    public bool CanBeTakenOver(MissionObject hull) => ((MissionShip)hull).CanBeTakenOver;
}
