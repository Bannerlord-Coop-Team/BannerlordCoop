#if DEBUG
using Common.Commands;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.order followme
// coop.debug.naval.order charge
// coop.debug.naval.order hold
/// <summary>
/// DEBUG: issues follow-me, charge or hold to the local main agent's formation through the player's order controller, as the
/// order menu does, so orders from the deck or the water can be staged without key input.
/// </summary>
public sealed class NavalOrderCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.order <followme|charge|hold>";

    public string Prefix => "coop.debug.naval";

    public string Name => "order";

    public string Description => "Selects the local main agent's formation in the player's order controller and issues follow-me, " +
        "charge or hold to it (vanilla's NavalOrderController turns them into ship orders), then reports the formation and its ship.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("order", "followme, charge or hold.", true),
    };

    // Issues the order to the main agent's formation alone and reports what vanilla made of it.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 1 || !TryParseOrder(args[0], out var orderType)) return Failed(Usage);

        var mission = Mission.Current;
        if (mission?.GetMissionBehavior<CoopNavalBattleBehavior>() == null) return Failed("No active coop naval battle mission.");

        var main = Agent.Main;
        if (main == null || !main.IsActive()) return Failed("There is no living main agent to give the order.");

        var formation = main.Formation;
        if (formation == null) return Failed("The main agent is in no formation to order.");

        var orderController = mission.PlayerTeam?.PlayerOrderController;
        if (orderController == null) return Failed("The player team has no order controller.");
        if (!orderController.IsFormationSelectable(formation))
            return Failed("The main agent's formation " + formation.FormationIndex + " can't be selected by the player.");

        orderController.ClearSelectedFormations();
        orderController.SelectFormation(formation);
        string thrown = Issue(orderController, orderType);
        orderController.ClearSelectedFormations();

        string report = Describe(mission, main, formation, orderType, thrown);
        return thrown == null ? new CoopCommandResult(true, report) : Failed(report);
    }

    /// <summary>Reads followme, charge or hold, case-insensitively, as the order types the order menu issues for them.</summary>
    internal static bool TryParseOrder(string value, out OrderType orderType)
    {
        orderType = OrderType.None;
        if (IsWord(value, "followme")) orderType = OrderType.FollowMe;
        else if (IsWord(value, "charge")) orderType = OrderType.Charge;
        else if (IsWord(value, "hold")) orderType = OrderType.StandYourGround;

        return orderType != OrderType.None;
    }

    // A case-insensitive keyword match.
    private static bool IsWord(string value, string word) => string.Equals(value?.Trim(), word, StringComparison.OrdinalIgnoreCase);

    // Vanilla's follow-me reads the main agent's formation ship, which a swimmer may lack, so a throw is reported, not raised.
    private static string Issue(OrderController orderController, OrderType orderType)
    {
        try
        {
            orderController.SetOrder(orderType);
            return null;
        }
        catch (Exception exception)
        {
            return exception.GetType().Name + ": " + exception.Message;
        }
    }

    // The order, the formation's movement order and the ship order of the formation's hull after vanilla handled it.
    private static string Describe(Mission mission, Agent main, Formation formation, OrderType orderType, string thrown)
    {
        var shipsLogic = mission.GetMissionBehavior<NavalShipsLogic>();
        var ship = shipsLogic != null && shipsLogic.GetShip(formation, out var formationHull) ? formationHull : null;
        var mainShip = main.GetComponent<AgentNavalComponent>()?.FormationShip;
        return "NAVAL_ORDER issued=" + orderType + " formation=" + formation.FormationIndex + " units=" + formation.CountOfUnits +
            " movementOrder=" + formation.GetReadonlyMovementOrderReference().OrderType +
            " ship=" + (ship?.Index.ToString() ?? "none") +
            " shipOrder=" + (ship != null && ship.IsShipOrderActive ? ship.ShipOrder.MovementOrderEnum.ToString() : "none") +
            " mainFormationShip=" + (mainShip?.Index.ToString() ?? "none") +
            " mainSwimming=" + IsSwimming(main) +
            (thrown == null ? "" : " threw=" + thrown);
    }

    // Swimming or diving, the movement modes vanilla's capture vacancy counts as off the deck.
    private static bool IsSwimming(Agent agent)
    {
        var mode = agent.MovementMode & AgentMovementMode.WaterDiving;
        return mode == AgentMovementMode.WaterSurface || mode == AgentMovementMode.WaterDiving;
    }

    // A failed command result with output.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
