#if DEBUG
using Common.Commands;
using GameInterface.Services.MapEvents;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.fire 5d5b e4f7
// coop.debug.naval.fire 5d5b e4f7 status
/// <summary>
/// DEBUG: aims a hull's ship ballista at another hull and shoots it at once through vanilla's Shoot, on the client simulating
/// the weapon, so the shot goes out by the same capture and replication path as an AI or player shot; status reports why the
/// AI would or would not shoot at that hull.
/// </summary>
public sealed class NavalShipFireCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.fire <weapon-shipId-prefix> <target-shipId-prefix> [status]";

    private const string StatusWord = "status";

    public string Prefix => "coop.debug.naval";

    public string Name => "fire";

    public string Description => "Snaps a hull's ship ballista onto another hull and shoots it now (it must be loaded, manned and " +
        "simulated here); status reports its state, ammo, gunner and the arc, line-of-fire and AI-disable checks vanilla's AI reads.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("weapon", "The start of the ship id of the hull whose ballista shoots, as coop.debug.naval.inspect lists it.", true),
        new ExpectedArgs("target", "The start of the ship id of the hull to shoot at.", true),
        new ExpectedArgs("status", "status, to report the checks without shooting.", false),
    };

    // Resolves both hulls, then reports on the weapon or aims and shoots it.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out var weaponPrefix, out var targetPrefix, out bool statusOnly)) return Failed(Usage);

        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || mission.GetMissionBehavior<CoopNavalBattleBehavior>() == null)
            return Failed("No active coop naval battle mission.");

        var registry = controller.MissionComponent.ShipRegistry;
        var shipIds = registry.Ships.Select(ship => ship.ShipId).ToList();
        var error = NavalReswapCoopCommand.TryResolveShipId(shipIds, weaponPrefix, out var weaponShipId);
        if (error != null) return Failed(error);
        error = NavalReswapCoopCommand.TryResolveShipId(shipIds, targetPrefix, out var targetShipId);
        if (error != null) return Failed(error);

        if (!registry.TryGet(weaponShipId, out var weaponInfo) || weaponInfo.Hull is not MissionShip hull
            || !registry.TryGet(targetShipId, out var targetInfo) || targetInfo.Hull is not MissionShip target)
            return Failed("Both hulls must be naval ships in the mission.");

        var weapon = hull.ShipSiegeWeapon;
        if (weapon == null) return Failed("Hull " + weaponShipId + " carries no ship weapon.");

        var point = AimPointOf(target);
        string checks = Checks(weapon, hull, target, point, weaponInfo.CurrentAuthority);
        if (statusOnly) return new CoopCommandResult(true, "NAVAL_FIRE_STATUS weapon=" + weaponShipId + " target=" + targetShipId + " " + checks);

        var refusal = FireRefusal(weaponInfo.CurrentAuthority == controller.Session.OwnControllerId,
            SiegeMissionAuthorityGate.IsMachineSimulatedLocally(weapon.Id.Id), weapon.PilotAgent != null,
            weapon.State == RangedSiegeWeapon.WeaponState.Idle, weapon.CanShootAtPoint(point), weaponInfo.CurrentAuthority);
        if (refusal != null) return Failed("NAVAL_FIRE weapon=" + weaponShipId + " refused: " + refusal + "; " + checks);
        if (!SnapAim(weapon, point)) return Failed("NAVAL_FIRE weapon=" + weaponShipId + " refused: the target is out of the bolt's reach; " + checks);

        bool fired = weapon.Shoot();
        return new CoopCommandResult(true, "NAVAL_FIRE weapon=" + weaponShipId + " target=" + targetShipId + " shot=" + fired +
            "; the bolt leaves on a later tick and goes out as a replicated shot (shipWeapons.shotsSent here, shotsReplayed on peers); " +
            checks);
    }

    /// <summary>Reads <c>weapon target</c> or <c>weapon target status</c>; anything else is a usage error.</summary>
    internal static bool TryParse(ICoopCommandArgs args, out string weaponPrefix, out string targetPrefix, out bool statusOnly)
    {
        weaponPrefix = targetPrefix = null;
        statusOnly = false;
        if (args == null || args.Count < 2 || args.Count > 3) return false;

        weaponPrefix = args[0];
        targetPrefix = args[1];
        statusOnly = args.Count == 3;
        return !statusOnly || string.Equals(args[2]?.Trim(), StatusWord, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Why this client can't shoot the weapon now, or null: only the client simulating the hull and its machine runs vanilla's
    /// shot (copies have Shoot blocked), Shoot records the gunner as the shooter, only a loaded weapon (Idle) shoots, and a point
    /// vanilla's CanShootAtPoint refuses would only get a clamped aim.
    /// </summary>
    internal static string FireRefusal(bool ownsHull, bool simulatesMachine, bool hasGunner, bool isLoaded, bool canShootAtPoint,
        string authority)
    {
        if (!ownsHull || !simulatesMachine) return "the weapon is simulated by " + authority + "; run the command there";
        if (!hasGunner) return "nobody mans the weapon";
        if (!isLoaded) return "the weapon is not loaded (not Idle)";
        if (!canShootAtPoint) return "the target is outside the weapon's arc or elevation, or a friendly hull is in the line (CanShootAtPoint)";
        return null;
    }

    // The point vanilla's AI targets on a hull (its ShipTargetMissionObject), or the hull's origin when it has none.
    private static Vec3 AimPointOf(MissionShip target)
    {
        var targetObject = target.GameEntity.GetFirstScriptInFamilyDescending<ShipTargetMissionObject>();
        return targetObject != null ? targetObject.GameEntity.GlobalPosition : target.GlobalFrame.origin;
    }

    // Puts the weapon's input, target and current aim on the point at once, as the AI's aim would settle there over frames.
    private static bool SnapAim(RangedSiegeWeapon weapon, Vec3 point)
    {
        weapon.CalculateLocalDirectionAndLocalAngleToShootTarget(point, out float direction, out float angle);
        if (direction >= MathF.PI) return false;

        weapon.GiveExactInput(direction, angle);
        weapon.TargetDirection = weapon.CurrentDirection = weapon._inputTargetX;
        weapon.TargetReleaseAngle = weapon.CurrentReleaseAngle = weapon._inputTargetY;
        weapon.ApplyAimChange();
        return true;
    }

    // The reads that decide whether vanilla's AI shoots at the hull (RangedSiegeWeapon.CanShootAtThreat and the 7 m stop range).
    private static string Checks(RangedSiegeWeapon weapon, MissionShip hull, MissionShip target, Vec3 point, string authority)
    {
        var targetObject = target.GameEntity.GetFirstScriptInFamilyDescending<ShipTargetMissionObject>();
        var pilot = weapon.PilotAgent;
        bool aiWouldShoot = targetObject != null && weapon.CanShootAtThreat(new Threat { TargetableObject = targetObject });
        return string.Format(CultureInfo.InvariantCulture,
            "authority={0} state={1} ammo={2} gunner={3} gunnerAi={4} disabledForAi={5} distance={6:0.0} inArc={7} aiWouldShoot={8} " +
            "targetObject={9}",
            authority, weapon.State, weapon.AmmoCount, pilot?.Index.ToString(CultureInfo.InvariantCulture) ?? "none",
            pilot?.IsAIControlled ?? false, weapon.IsDisabledForBattleSideAI(hull.BattleSide),
            weapon.GameEntity.GlobalPosition.Distance(point), weapon.CanShootAtPoint(point), aiWouldShoot, targetObject != null);
    }

    // A failed command result with output.
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
