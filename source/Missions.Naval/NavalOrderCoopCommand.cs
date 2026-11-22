#if DEBUG
using Common.Commands;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.order followme
// coop.debug.naval.order charge
// coop.debug.naval.order hold
/// <summary>
/// DEBUG: issues follow-me to every formation select-all picks, or charge or hold to the local main agent's formation, through
/// the player's order controller as the order menu does, so orders from the deck or the water can be staged without key input.
/// </summary>
public sealed class NavalOrderCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.order <followme|charge|hold>";

    public string Prefix => "coop.debug.naval";

    public string Name => "order";

    public string Description => "Issues follow-me to the formations vanilla's select-all picks (every selectable formation but the one " +
        "whose hull the main agent captains, since a ship can't follow itself), or charge or hold to the main agent's formation alone, " +
        "through the player's order controller (vanilla's NavalOrderController turns them into ship orders), then reports the main " +
        "formation, its ship and each selected formation's ship order. Follow-me shows a ship follow order only with a second hull.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("order", "followme, charge or hold.", true),
    };

    // Issues the order to its formations and reports what vanilla made of it.
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
        if (orderType != OrderType.FollowMe && !orderController.IsFormationSelectable(formation))
            return Failed("The main agent's formation " + formation.FormationIndex + " can't be selected by the player.");

        var previousSelection = orderController.SelectedFormations.ToList();
        var selected = Select(orderController, formation, orderType);
        if (selected.Count == 0)
        {
            RestoreSelection(orderController, previousSelection);
            return Failed("The player can select no formation to order.");
        }

        string thrown = Issue(orderController, orderType);
        RestoreSelection(orderController, previousSelection);

        string report = Describe(mission, main, formation, selected, orderType, thrown);
        return thrown == null ? new CoopCommandResult(true, report) : Failed(report);
    }

    // Follow-me goes to vanilla's select-all, which leaves out the hull the main agent captains (it is the hull followed);
    // charge and hold go to the main agent's formation alone.
    private static List<Formation> Select(OrderController orderController, Formation mainFormation, OrderType orderType)
    {
        orderController.ClearSelectedFormations();
        if (orderType == OrderType.FollowMe)
            orderController.SelectAllFormations();
        else
            orderController.SelectFormation(mainFormation);

        return orderController.SelectedFormations.ToList();
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

    // Puts back the formations the player had selected in the order UI, as far as they are still selectable.
    private static void RestoreSelection(OrderController orderController, IEnumerable<Formation> previousSelection)
    {
        orderController.ClearSelectedFormations();
        foreach (var formation in previousSelection.Where(orderController.IsFormationSelectable))
            orderController.SelectFormation(formation);
    }

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

    // The order, the main formation's movement order and ship order, and each selected formation's ship order after vanilla.
    private static string Describe(Mission mission, Agent main, Formation formation, IEnumerable<Formation> selected,
        OrderType orderType, string thrown)
    {
        var shipsLogic = mission.GetMissionBehavior<NavalShipsLogic>();
        var ship = ShipOf(shipsLogic, formation);
        var mainShip = main.GetComponent<AgentNavalComponent>()?.FormationShip;
        return "NAVAL_ORDER issued=" + orderType + " formation=" + formation.FormationIndex + " units=" + formation.CountOfUnits +
            " movementOrder=" + formation.GetReadonlyMovementOrderReference().OrderType +
            " ship=" + (ship?.Index.ToString() ?? "none") +
            " shipOrder=" + ShipOrderOf(ship) +
            " mainFormationShip=" + (mainShip?.Index.ToString() ?? "none") +
            " mainSwimming=" + IsSwimming(main) +
            " selected=[" + string.Join(",", selected.Select(other =>
                other.FormationIndex + ":ship=" + (ShipOf(shipsLogic, other)?.Index.ToString() ?? "none") + ":shipOrder=" +
                ShipOrderOf(ShipOf(shipsLogic, other)))) + "]" +
            (thrown == null ? "" : " threw=" + thrown);
    }

    // The hull a formation sails, or null.
    private static MissionShip ShipOf(NavalShipsLogic shipsLogic, Formation formation) =>
        shipsLogic != null && shipsLogic.GetShip(formation, out var hull) ? hull : null;

    // A hull's active ship order, or none.
    private static string ShipOrderOf(MissionShip ship) =>
        ship != null && ship.IsShipOrderActive ? ShipOrderName(ship.ShipOrder.MovementOrderEnum) : "none";

    /// <summary>
    /// The ship order's name; Follow shares its value with the StaticOrderCount marker, which enum ToString picks first.
    /// </summary>
    internal static string ShipOrderName(ShipOrder.ShipMovementOrderEnum order) =>
        order == ShipOrder.ShipMovementOrderEnum.Follow ? nameof(ShipOrder.ShipMovementOrderEnum.Follow) : order.ToString();

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
