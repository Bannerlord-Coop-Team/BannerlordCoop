#if DEBUG
using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Places the invoking owner's hull facing a selected foreign enemy's broadside for a real ram.</summary>
public interface INavalRamStageCoopCommand : ICoopCommand { }

/// <inheritdoc cref="INavalRamStageCoopCommand"/>
public sealed class NavalRamStageCoopCommand : INavalRamStageCoopCommand
{
    private readonly INavalDebugHulls hulls;
    private readonly INavalRamStaging staging;

    /// <summary>Uses the own-hull selection and measured ram approach geometry.</summary>
    public NavalRamStageCoopCommand(INavalDebugHulls hulls, INavalRamStaging staging)
    {
        this.hulls = hulls;
        this.staging = staging;
    }

    public string Prefix => "coop.debug.naval";
    public string Name => "ram_stage";
    public string Description => "Launches the own ram tip 6 m from a foreign enemy broadside at 10 m/s relative closing speed.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "Foreign enemy hull id from inspect summary.", true),
    };

    /// <summary>Runs on the hull owner, because the dedicated server does not simulate hull poses.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 1) return Failed("Usage: coop.debug.naval.ram_stage <shipId-prefix>");
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        var ships = mission?.GetMissionBehavior<NavalShipsLogic>();
        if (controller == null || ships == null) return Failed("No active coop naval battle mission.");
        if (!mission.IsDeploymentFinished) return Failed("Finish deployment before staging a ram.");
        var error = hulls.TryGetOwnHull(out var own);
        if (error != null) return Failed(error);
        var registry = controller.MissionComponent.ShipRegistry;
        error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), args[0], out var id);
        if (error != null) return Failed(error);
        var targetInfo = registry.Ships.Single(ship => ship.ShipId == id);
        var target = targetInfo.Hull as MissionShip;
        if (targetInfo.CurrentAuthority == controller.Session.OwnControllerId) return Failed("Target must belong to another authority.");
        if (target == null || target.IsRemoved || target.IsSinking || own.IsSinking) return Failed("Both hulls must be afloat.");
        if (own.Team == null || target.Team == null || !own.Team.IsEnemyOf(target.Team)) return Failed("Target must be an enemy hull.");
        if (own._ram == null) return Failed("The own hull has no ram. Stage the scenario with ram=1.");
        if (own.GetIsConnected() || target.GetIsConnected()) return Failed("Disconnect ropes before staging a ram.");

        var capsule = own._ram.GetRamCapsuleData(0f, getDataForNextFrame: false);
        var ramTip = own.GlobalFrame.TransformToLocal(capsule.P2);
        var bounds = GameEntityPhysicsExtensions.GetLocalPhysicsBoundingBox(target.GameEntity, false);
        var targetFrame = target.GlobalFrame;
        var contact = targetFrame.TransformToParent(new Vec3(bounds.max.x, bounds.center.y, 0f));
        var targetVelocity = target.GameEntity.GetLinearVelocityAtGlobalPointForEntityWithDynamicBody(contact);
        var approach = staging.Approach(targetFrame, bounds.min, bounds.max, ramTip, capsule.Radius, targetVelocity);
        NavalHelmOverride.Clear();
        own.SetAnchor(false);
        ships.TeleportShip(own, approach.Frame, checkFreeArea: false);
        own.GameEntity.SetAngularVelocity(Vec3.Zero);
        own.GameEntity.SetLinearVelocity(approach.Velocity);
        return new CoopCommandResult(true, $"NAVAL_RAM_STAGE ship={hulls.ShipIdOf(own)} target={id} targetAuthority={targetInfo.CurrentAuthority} gap=6 closingSpeed=10; launched, inspect both clients after 2 s (no helm or settling wait)");
    }

    /// <summary>Reports a refused staging request without changing either hull.</summary>
    private static CoopCommandResult Failed(string error) => new CoopCommandResult(false, error, "command_failed");
}
#endif
