#if DEBUG
using Common.Commands;
using GameInterface.Services.MapEvents.NavalScenarios;
using Missions.Battles;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Places the invoking owner's hull facing a selected foreign enemy's broadside for a real ram.</summary>
public interface INavalRamStageCoopCommand : ICoopCommand { }

/// <inheritdoc cref="INavalRamStageCoopCommand"/>
public sealed class NavalRamStageCoopCommand : INavalRamStageCoopCommand
{
    private readonly INavalDebugHulls hulls;

    /// <summary>Uses the existing own-hull selection and scenario placement geometry.</summary>
    public NavalRamStageCoopCommand(INavalDebugHulls hulls) => this.hulls = hulls;

    public string Prefix => "coop.debug.naval";
    public string Name => "ram_stage";
    public string Description => "Places the own hull 15 m from a foreign enemy hull, bow toward its broadside.";
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

        var targetFrame = target.GlobalFrame;
        var pose = NavalScenarioMissionLogic.PlacementPose(NavalScenarioPlacement.Ramming, targetFrame.origin.AsVec2,
            targetFrame.rotation.f.AsVec2, own.MissionShipObject.DeploymentArea, target.MissionShipObject.DeploymentArea, 15f, true);
        var frame = MatrixFrame.Identity;
        frame.rotation.f = pose.Direction.ToVec3().NormalizedCopy();
        frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        frame.origin = pose.Position.ToVec3();
        NavalHelmOverride.Clear();
        ships.TeleportShip(own, frame, checkFreeArea: false);
        return new CoopCommandResult(true, $"NAVAL_RAM_STAGE ship={hulls.ShipIdOf(own)} target={id} targetAuthority={targetInfo.CurrentAuthority} gap=15; settle 2 s, then helm 0 1 1 30");
    }

    /// <summary>Reports a refused staging request without changing either hull.</summary>
    private static CoopCommandResult Failed(string error) => new CoopCommandResult(false, error, "command_failed");
}
#endif
