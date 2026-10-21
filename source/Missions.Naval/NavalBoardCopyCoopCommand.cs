#if DEBUG
using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.board_copy
// coop.debug.naval.board_copy c13e
// coop.debug.naval.board_copy status
/// <summary>
/// DEBUG: teleports this client's main agent onto another owner's hull (a copy here) where vanilla puts an agent on a
/// deck, and reports the deck error, so the main-agent-on-a-copy check needs no boarding fight.
/// </summary>
public sealed class NavalBoardCopyCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.board_copy [shipId-prefix | status]";

    private readonly INavalShipEngine engine;

    public NavalBoardCopyCoopCommand(INavalShipEngine engine)
    {
        this.engine = engine;
    }

    public string Prefix => "coop.debug.naval";

    public string Name => "board_copy";

    public string Description => "Teleports the main agent onto a copy's deck (the nearest copy, or the one whose ship id starts with " +
        "the prefix) at vanilla's deck spot and reports the deck error; status reports the hull the main agent stands on.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("target", "status, or the start of a copy's ship id as coop.debug.naval.inspect lists it; the copy " +
            "nearest the main agent when omitted.", false),
    };

    // Boards the named or nearest copy, or reports where the main agent stands.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args != null && args.Count > 1) return Failed(Usage);

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");
        if (!mission.IsDeploymentFinished) return Failed("The battle is still deploying; board once deployment has finished.");

        var main = Agent.Main;
        if (main == null || !main.IsActive()) return Failed("There is no living main agent to board with.");

        string target = args != null && args.Count == 1 ? args[0].Trim() : null;
        var registry = controller.MissionComponent.ShipRegistry;
        if (string.Equals(target, "status", StringComparison.OrdinalIgnoreCase)) return Status(registry, main);

        var error = TryResolveCopy(registry, target, main.Position, out var hull, out var ship);
        if (error != null) return Failed(error);

        return Board(main, hull, ship);
    }

    // The copy named by the id prefix, or the afloat registered copy nearest to position; why there is none, or null.
    private static string TryResolveCopy(INetworkShipRegistry registry, string prefix, Vec3 position, out MissionShip hull,
        out NetworkShipInfo ship)
    {
        hull = null;
        ship = null;
        var copies = registry.Ships.Where(info => info.Hull is MissionShip copy && NavalForeignHulls.Contains(copy)
            && !copy.IsRemoved && !copy.IsSinking).ToArray();
        if (copies.Length == 0) return "This client has no afloat copy of another owner's hull.";

        if (string.IsNullOrEmpty(prefix))
        {
            ship = copies.OrderBy(info => ((MissionShip)info.Hull).GlobalFrame.origin.DistanceSquared(position)).First();
            hull = (MissionShip)ship.Hull;
            return null;
        }

        var error = NavalReswapCoopCommand.TryResolveShipId(copies.Select(info => info.ShipId), prefix, out var shipId);
        if (error != null) return error + " (only afloat copies of another owner's hull count)";

        ship = copies.First(info => info.ShipId == shipId);
        hull = (MissionShip)ship.Hull;
        return null;
    }

    // Leaves any used station and teleports the main agent to the copy's deck spot, facing along it, as vanilla places a player.
    private static CoopCommandResult Board(Agent main, MissionShip hull, NetworkShipInfo ship)
    {
        var spot = DeckSpot(hull, out string spotName);
        if (main.CurrentlyUsedGameObject != null) main.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);

        main.TeleportToPosition(spot.origin);
        var direction = spot.rotation.f.AsVec2.Normalized();
        main.SetMovementDirection(in direction);

        var hullFrame = hull.GlobalFrame;
        var position = main.Position;
        return new CoopCommandResult(true, string.Format(CultureInfo.InvariantCulture,
            "NAVAL_BOARD ship={0} owner={1} spot={2} deckError={3:0.00} m heightAboveSpot={4:0.00} m; the stepped hull resolves " +
            "on the next tick (board_copy status), and coop.debug.naval.inspect on {1} reports the puppet's maxDeckError",
            ship.ShipId, ship.CurrentAuthority, spotName, DeckError(hullFrame, spot.origin, position), position.z - spot.origin.z));
    }

    // Vanilla's deck spots: the player standing point NavalShipAgents teleports Agent.Main to, else the middle inner deck
    // frame the DLC storyline teleports Agent.Main to (MissionShip.GetMiddleInnerSpawnGlobalFrame).
    private static MatrixFrame DeckSpot(MissionShip hull, out string spotName)
    {
        if (hull.HasPlayerStandingPointEntity)
        {
            spotName = "player_standing_point";
            return hull.PlayerStandingPointEntity.GetGlobalFrame();
        }

        spotName = "middle_inner_deck";
        return hull.GetMiddleInnerSpawnGlobalFrame();
    }

    // The hull the main agent stands on now, its owner and the agent's hull-local position.
    private CoopCommandResult Status(INetworkShipRegistry registry, Agent main)
    {
        var support = engine.GetSupportHull(main) as MissionShip;
        if (support == null || !registry.TryGetByHull(support, out var ship))
            return new CoopCommandResult(true, "NAVAL_BOARD_STATUS supportShip=none: the main agent stands on no registered hull");

        var local = support.GlobalFrame.TransformToLocalNonOrthogonal(main.Position);
        return new CoopCommandResult(true, string.Format(CultureInfo.InvariantCulture,
            "NAVAL_BOARD_STATUS supportShip={0} owner={1} copy={2} local=({3:0.00}, {4:0.00}, {5:0.00})",
            ship.ShipId, ship.CurrentAuthority, NavalForeignHulls.Contains(support), local.x, local.y, local.z));
    }

    /// <summary>
    /// The horizontal distance in metres between the deck spot and the agent, measured in the hull's frame as the deck
    /// interpolator's <c>NavalDeckTarget.HorizontalError</c> does.
    /// </summary>
    internal static float DeckError(in MatrixFrame hull, Vec3 spot, Vec3 position) =>
        (hull.TransformToLocalNonOrthogonal(position) - hull.TransformToLocalNonOrthogonal(spot)).AsVec2.Length;

    // A failed command result with output.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
