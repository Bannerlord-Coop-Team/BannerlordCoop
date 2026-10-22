#if DEBUG
using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System.Globalization;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.swim 1d93
/// <summary>
/// DEBUG: drops this client's main agent into the water off a hull's starboard side, so a swimming player (orders from the
/// water, a victim playing on without a deck, a swimmer's capture vacancy) can be staged.
/// </summary>
public sealed class NavalSwimCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.swim <shipId-prefix>";

    /// <summary>How far past the hull's side the agent lands, clear of its oars and ropes.</summary>
    internal const float SideClearance = 4f;

    public string Prefix => "coop.debug.naval";

    public string Name => "swim";

    public string Description => "Puts the local main agent in the water " + SideClearance.ToString(CultureInfo.InvariantCulture) +
        " m off a hull's starboard side, leaving any station it used, so swimming and orders from the water can be tested.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "The start of the hull's ship id, as coop.debug.naval.inspect lists it.", true),
    };

    // Moves the main agent to the water surface beside the one registered hull whose ship id starts with the prefix.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 1) return Failed(Usage);

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");

        var main = Agent.Main;
        if (main == null || !main.IsActive()) return Failed("There is no living main agent to put in the water.");

        var registry = controller.MissionComponent.ShipRegistry;
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), args[0], out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull || hull.Physics == null || !hull.GameEntity.IsValid)
            return Failed("The hull is not a naval ship in the mission.");

        var box = hull.Physics.PhysicsBoundingBoxWithoutChildren;
        var position = hull.GlobalFrame.TransformToParent(SideOffset(box.min.AsVec2, box.max.AsVec2));
        position.z = mission.Scene.GetWaterLevelAtPosition(position.AsVec2, true, false);

        if (main.CurrentlyUsedGameObject != null) main.StopUsingGameObject(isSuccessful: true, Agent.StopUsingGameObjectFlags.None);
        main.TeleportToPosition(position);

        return new CoopCommandResult(true, string.Format(CultureInfo.InvariantCulture,
            "NAVAL_SWIM ship={0} position=({1:0.0}, {2:0.0}, {3:0.00}); the engine switches the agent to swimming on its next ticks",
            shipId, position.x, position.y, position.z));
    }

    /// <summary>The point in the hull's local frame just off its starboard side (local +x), midway along it, at the waterline.</summary>
    internal static Vec3 SideOffset(Vec2 min, Vec2 max) => new Vec3(max.x + SideClearance, (min.y + max.y) * 0.5f, 0f);

    // A failed command result with output.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
