#if DEBUG
using Common.Commands;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.damage 1d93 500
/// <summary>
/// DEBUG: deals hull damage to a registered hull the way a ship siege hit does, so from a non-owner it is routed to the
/// hull's owner like a real hit.
/// </summary>
public sealed class NavalDamageCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";

    public string Name => "damage";

    public string Description => "Deals hull damage to a hull as a ship siege hit would; another owner's hull gets it through its owner.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "The start of the hull's ship id, as coop.debug.naval.inspect lists it.", true),
        new ExpectedArgs("amount", "Raw hull damage, before the owner's campaign ship damage model.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 2 || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float amount)
            || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
            return Failed("Usage: coop.debug.naval.damage <shipId-prefix> <amount>, with a positive amount");

        return Hit(args[0], _ => amount, "NAVAL_DAMAGE");
    }

    /// <summary>[Game thread] Hits the one registered hull whose ship id starts with <paramref name="prefix"/>.</summary>
    internal static CoopCommandResult Hit(string prefix, Func<MissionShip, float> amountOf, string label)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");

        var registry = controller.MissionComponent.ShipRegistry;
        var error = NavalReswapCoopCommand.TryResolveShipId(registry.Ships.Select(ship => ship.ShipId), prefix, out var shipId);
        if (error != null) return Failed(error);
        if (!registry.TryGet(shipId, out var info) || info.Hull is not MissionShip hull) return Failed("The hull is not a naval ship.");

        float amount = amountOf(hull);
        float before = hull.HitPoints;
        bool own = info.CurrentAuthority == controller.Session.OwnControllerId;
        NavalShipDamageGate.DealLocalHit(hull, amount);

        string outcome = own
            ? "applied here: hp " + before.ToString("0.#", CultureInfo.InvariantCulture) + " -> " +
              hull.HitPoints.ToString("0.#", CultureInfo.InvariantCulture) + ", sinking " + hull.IsSinking
            : "routed to its owner " + info.CurrentAuthority + " (shipDamage.sent here, shipDamage.applied there; hp follows its condition)";
        return new CoopCommandResult(true, label + " ship=" + shipId + " amount=" + amount.ToString("0.#", CultureInfo.InvariantCulture) +
            " " + outcome);
    }

    internal static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}

// coop.debug.naval.sink 1d93
/// <summary>DEBUG: sinks a registered hull through the same hull-hit path as <see cref="NavalDamageCoopCommand"/>.</summary>
public sealed class NavalSinkCoopCommand : ICoopCommand
{
    // The owner's damage model may reduce raw damage; ten times the hull's maximum always leaves it at 0 HP.
    private const float MaxHitPointsMultiple = 10f;

    public string Prefix => "coop.debug.naval";

    public string Name => "sink";

    public string Description => "Sinks a hull with one hull hit that takes all its HP; another owner's hull sinks through its owner.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "The start of the hull's ship id, as coop.debug.naval.inspect lists it.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 1) return NavalDamageCoopCommand.Failed("Usage: coop.debug.naval.sink <shipId-prefix>");

        return NavalDamageCoopCommand.Hit(args[0], hull => hull.ShipOrigin.MaxHitPoints * MaxHitPointsMultiple, "NAVAL_SINK");
    }
}
#endif
