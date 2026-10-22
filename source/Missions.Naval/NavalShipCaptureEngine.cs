using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipControl;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <inheritdoc cref="INavalShipCaptureEngine"/>
public class NavalShipCaptureEngine : INavalShipCaptureEngine
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalShipCaptureEngine>();

    private static NavalShipsLogic ShipsLogic => Mission.Current?.GetMissionBehavior<NavalShipsLogic>();

    public void ApplyCapture(MissionObject prize, MissionObject captorHull)
    {
        var ship = (MissionShip)prize;
        var captor = (MissionShip)captorHull;
        var shipsLogic = ShipsLogic;
        if (shipsLogic == null || captor.Formation == null)
        {
            Logger.Warning("[NavalSync] Could not replay the capture of hull {Index}: the captor's hull {CaptorIndex} has no formation here",
                ship.Index, captor.Index);
            return;
        }

        shipsLogic.OnShipCaptured(ship, captor.Formation);
        // Vanilla's swap hands both hulls an auto-updated controller; a copy keeps none, so it never steers itself here.
        if (NavalForeignHulls.Contains(ship)) ship.SetController(ShipControllerType.None, autoUpdateController: false);
        if (NavalForeignHulls.Contains(captor)) captor.SetController(ShipControllerType.None, autoUpdateController: false);
        // As the helm's capture branch does once the swap ran.
        ship.InvalidateActiveFormationTroopOnShipCache();
        captor.InvalidateActiveFormationTroopOnShipCache();
    }

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
