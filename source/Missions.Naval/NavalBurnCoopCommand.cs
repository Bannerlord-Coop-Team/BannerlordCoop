#if DEBUG
using Common.Commands;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Burns a registered hull on its authority through the existing fire-hit apply path.</summary>
public interface INavalBurnCoopCommand : ICoopCommand { }

/// <inheritdoc cref="INavalBurnCoopCommand"/>
public sealed class NavalBurnCoopCommand : INavalBurnCoopCommand
{
    private readonly INavalShipEngine engine;

    /// <summary>Uses the same fire-hit implementation as received combat damage.</summary>
    public NavalBurnCoopCommand(INavalShipEngine engine) => this.engine = engine;

    public string Prefix => "coop.debug.naval";
    public string Name => "burn";
    public string Description => "Burns a hull through fire damage; run on the client listed as its authority.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "The hull's full ship id or unique prefix from coop.debug.naval.inspect.", true),
    };

    /// <summary>[Game thread] Resolves and checks authority before exhausting fire HP through the combat fire path.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 1 || string.IsNullOrWhiteSpace(args[0]))
            return Failed("Usage: coop.debug.naval.burn <shipId-prefix>");

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");

        var registry = controller.MissionComponent.ShipRegistry;
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), args[0], out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull)
            return Failed("The hull is not a naval ship.");

        // Hull simulation belongs to its client authority, including NPC hulls, rather than the dedicated server.
        if (info.CurrentAuthority != controller.Session.OwnControllerId)
            return Failed("NAVAL_BURN ship=" + shipId + " refused: run on hull authority " + info.CurrentAuthority);
        if (!hull.GameEntity.IsValid || hull.IsDisabled || hull.IsSinking || hull.IsSunk)
            return Failed("NAVAL_BURN ship=" + shipId + " refused: hull is disabled or sinking");
        if (hull.FireHitPoints <= 0f)
            return Failed("NAVAL_BURN ship=" + shipId + " refused: hull is already burned");

        float before = hull.FireHitPoints;
        float multiplier = MissionGameModels.Current.AgentApplyDamageModel.CalculateHullFireDamage(1f, hull.ShipOrigin);
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
            return Failed("NAVAL_BURN ship=" + shipId + " refused: fire damage model gives no finite positive damage");

        // NavalAgentApplyDamageModel scales raw fire damage by the figurehead's factor. Overshoot avoids rounding above zero.
        float damage = (before / multiplier) * 2f;
        if (float.IsNaN(damage) || float.IsInfinity(damage))
            return Failed("NAVAL_BURN ship=" + shipId + " refused: required fire damage is not finite");

        engine.ApplyShipDamage(hull, new NetworkApplyShipDamage(shipId, controller.Session.OwnControllerId,
            BattleShipDamageKind.Fire, damage, 0f, Vec3.Zero, Guid.Empty, Guid.Empty, false, -1), null, null);
        return new CoopCommandResult(hull.FireHitPoints <= 0f,
            "NAVAL_BURN ship=" + shipId + " authority=" + info.CurrentAuthority +
            " fireHpBefore=" + before.ToString("0.###", CultureInfo.InvariantCulture) +
            " fireHpAfter=" + hull.FireHitPoints.ToString("0.###", CultureInfo.InvariantCulture) +
            " controller=" + (hull.Controller?.ControllerType.ToString() ?? "None"),
            hull.FireHitPoints <= 0f ? null : "command_failed");
    }

    /// <summary>Reports a refusal without changing the hull.</summary>
    private CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
