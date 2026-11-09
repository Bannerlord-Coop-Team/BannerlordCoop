#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.MapEvents;
using Missions.Agents;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Missiles;

internal static class MissileDebugCommands
{
    public sealed class ForceBounceMissilesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.missile";
        public string Name => "force_bounce";
        public string Description => "Makes the next owned ground/object sticks use vanilla bounce physics.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = { new ExpectedArgs("count", "Collisions 1..500.") };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count != 1 || !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                || count < 1 || count > 500)
                return new CoopCommandResult(false, "Expected count 1..500.", "invalid_arguments");
            var handler = Mission.Current?.GetMissionBehavior<CoopMissionController>()?.MissionComponent.MissileHandler;
            if (ModInformation.IsServer || handler == null)
                return new CoopCommandResult(false, "An active co-op client mission is required.", "no_mission");
            handler.ForcedBouncesRemaining = count;
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new { remaining = handler.ForcedBouncesRemaining }));
        }
    }

    public sealed class FireMissilesAtCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.missile";
        public string Name => "fire_at";
        public string Description => "Fires owned agents' equipped projectiles through the normal agent-shot callback.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } =
        {
            new ExpectedArgs("target", "Mission object id or invariant x,y,z coordinates."),
            new ExpectedArgs("count", "Missiles 1..50.")
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count != 2 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                || count < 1 || count > 50)
                return new CoopCommandResult(false, "Expected target and count 1..50.", "invalid_arguments");
            var mission = Mission.Current;
            var agents = mission?.GetMissionBehavior<CoopMissionController>()?.MissionComponent.AgentRegistry;
            if (ModInformation.IsServer || agents == null)
                return new CoopCommandResult(false, "An active co-op client mission is required.", "no_mission");
            Vec3 target;
            if (int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            {
                var obj = mission.MissionObjects.FirstOrDefault(value => value.Id.Id == id);
                if (obj == null) return new CoopCommandResult(false, "Mission object was not found.", "target_not_found");
                target = obj.GameEntity.GetGlobalFrame().origin;
            }
            else
            {
                string[] xyz = args[0].Split(',');
                if (xyz.Length != 3
                    || !float.TryParse(xyz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    || !float.TryParse(xyz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                    || !float.TryParse(xyz[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)
                    || float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)
                    || float.IsNaN(z) || float.IsInfinity(z))
                    return new CoopCommandResult(false, "Expected a mission object id or finite x,y,z coordinates.", "invalid_arguments");
                target = new Vec3(x, y, z);
            }

            var ready = new List<(Agent Agent, EquipmentIndex Slot)>();
            var fallback = new List<(Agent Agent, EquipmentIndex Slot)>();
            foreach (Agent agent in mission.Agents)
            {
                if (!agent.IsActive() || !agents.IsLocallyControlled(agent)) continue;
                EquipmentIndex wielded = agent.GetPrimaryWieldedItemIndex();
                for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
                {
                    MissionWeapon weapon = agent.Equipment[slot];
                    if (weapon.IsEmpty || weapon.CurrentUsageItem?.IsRangedWeapon != true) continue;
                    MissionWeapon projectile = weapon.CurrentUsageItem.IsConsumable ? weapon : weapon.AmmoWeapon;
                    if (projectile.IsEmpty || projectile.Amount <= 0) continue;
                    if (slot == wielded) ready.Add((agent, slot));
                    else fallback.Add((agent, slot));
                }
            }
            if (ready.Count == 0 && fallback.Count > 0)
                ready.Add(fallback.OrderBy(value => (value.Agent.Position - target).LengthSquared).First());
            if (ready.Count == 0)
                return new CoopCommandResult(false, "No locally controlled ranged agent has ammunition.", "no_shooter");

            var used = new Dictionary<Agent, int>();
            int fired = 0;
            for (int i = 0; i < count; i++)
            {
                var shooter = ready[i % ready.Count];
                var weapon = shooter.Agent.Equipment[shooter.Slot];
                if (shooter.Agent.GetPrimaryWieldedItemIndex() != shooter.Slot)
                    shooter.Agent.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, shooter.Slot, true, false, weapon.CurrentUsageIndex);
                float speed = weapon.GetModifiedMissileSpeedForCurrentUsage();
                if (!(speed > 0f)) continue;
                Vec3 origin = shooter.Agent.GetEyeGlobalPosition();
                Vec3 toTarget = target - origin;
                Vec3 direction = toTarget;
                if (direction.Normalize() < 0.01f) continue;
                double horizontal = Math.Sqrt(((double)toTarget.x * toTarget.x) + ((double)toTarget.y * toTarget.y));
                if (horizontal > 0.01)
                {
                    MissionWeapon projectile = weapon.CurrentUsageItem.IsConsumable ? weapon : weapon.AmmoWeapon;
                    WeaponStatsData stats = projectile.GetWeaponStatsDataForUsage(projectile.CurrentUsageIndex);
                    var usage = projectile.CurrentUsageItem;
                    float angle = Mission.GetMissileVerticalAimCorrection(toTarget, speed, ref stats,
                        ItemObject.GetAirFrictionConstant(usage.WeaponClass, usage.WeaponFlags));
                    if (float.IsNaN(angle) || float.IsInfinity(angle)) continue;
                    direction = new Vec3((float)(toTarget.x / horizontal * Math.Cos(angle)),
                        (float)(toTarget.y / horizontal * Math.Cos(angle)), (float)Math.Sin(angle));
                }
                Vec3 side = Vec3.CrossProduct(direction, Math.Abs(direction.z) > 0.95f ? Vec3.Forward : Vec3.Up);
                side.Normalize();
                var orientation = new Mat3(side, direction, Vec3.CrossProduct(side, direction));
                int before = mission._missilesDictionary.Count;
                mission.OnAgentShootMissile(shooter.Agent, shooter.Slot, origin, direction * speed, orientation,
                    false, true, -1);
                if (mission._missilesDictionary.Count == before) continue;
                fired++;
                used.TryGetValue(shooter.Agent, out int previous);
                used[shooter.Agent] = previous + 1;
            }
            var rows = used.Select(value => new
            {
                agentId = agents.TryGetAgentInfo(value.Key, out var info) ? info.AgentId.ToString("N") : null,
                agentIndex = value.Key.Index, missiles = value.Value
            }).ToArray();
            string output = "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
            {
                target = new { x = target.x, y = target.y, z = target.z }, missiles = fired, shooters = rows
            });
            return fired == count ? new CoopCommandResult(true, output)
                : new CoopCommandResult(false, output, "shot_failed");
        }
    }

    private static IEnumerable<(MissionObject Object, DestructableComponent Destruction)> ReadDestructibles(Mission mission)
    {
        var seen = new HashSet<DestructableComponent>();
        foreach (var machine in mission.MissionObjects.OfType<UsableMachine>())
            if (machine.DestructionComponent != null && seen.Add(machine.DestructionComponent))
                yield return (machine, machine.DestructionComponent);
        foreach (var destruction in mission.MissionObjects.OfType<DestructableComponent>())
            if (seen.Add(destruction)) yield return (destruction, destruction);
    }

    public sealed class MissileDestructiblesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.missile";
        public string Name => "destructibles";
        public string Description => "Lists destructible mission objects and their current state without changing them.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count != 0) return new CoopCommandResult(false, "Expected no arguments.", "invalid_arguments");
            if (ModInformation.IsServer || Mission.Current == null)
                return new CoopCommandResult(false, "An active client mission is required.", "no_mission");
            var rows = ReadDestructibles(Mission.Current).OrderBy(value => value.Object.Id.Id).Select(value =>
            {
                Vec3 position = value.Object.GameEntity.GetGlobalFrame().origin;
                return new { missionObjectId = value.Object.Id.Id, type = value.Object.GetType().Name,
                    state = value.Destruction._currentStateIndex, hitPoints = value.Destruction.HitPoint,
                    position = new { x = position.x, y = position.y, z = position.z } };
            }).ToArray();
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new { count = rows.Length, rows }));
        }
    }

    public sealed class DestroyMissileParentCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.missile";
        public string Name => "destroy";
        public string Description => "Destroys a siege machine through its normal hit path on the elected siege host.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = { new ExpectedArgs("missionObjectId", "Id from destructibles.") };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count != 1 || !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id))
                return new CoopCommandResult(false, "Expected a mission object id.", "invalid_arguments");
            var mission = Mission.Current;
            var controller = mission?.GetMissionBehavior<CoopBattleController>();
            if (ModInformation.IsServer || mission?.IsSiegeBattle != true || controller?.Session?.IsLocalHost != true
                || !SiegeMissionAuthorityGate.IsLocalAuthority || controller.DebugSiegeMachineState == null)
                return new CoopCommandResult(false, "Run on the elected host of the active co-op siege.", "not_siege_host");
            var target = ReadDestructibles(mission).FirstOrDefault(value => value.Object.Id.Id == id
                || value.Destruction.Id.Id == id);
            if (target.Destruction == null)
                return new CoopCommandResult(false, "Destructible was not found.", "target_not_found");
            if (!(target.Object is UsableMachine))
                return new CoopCommandResult(false, "This destructible has no replicated siege machine owner.", "unsupported_target");

            var destruction = target.Destruction;
            int before = destruction._currentStateIndex;
            if (!destruction.IsDestroyed)
            {
                var hit = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
                    .Where(item => item.Weapons != null)
                    .SelectMany(item => item.Weapons.Select((usage, index) => new { item, usage, index }))
                    .FirstOrDefault(value => !destruction.DestroyedByStoneOnly
                        || (value.usage.WeaponFlags.HasAnyFlag(WeaponFlags.NotUsableWithOneHand)
                            && (value.usage.WeaponClass == WeaponClass.Sling || value.usage.WeaponClass == WeaponClass.Stone
                                || value.usage.WeaponClass == WeaponClass.Boulder || value.usage.WeaponClass == WeaponClass.BallistaBoulder
                                || value.usage.WeaponClass == WeaponClass.BallistaStone)));
                if (hit == null) return new CoopCommandResult(false, "No suitable hit weapon is loaded.", "no_weapon");
                var weapon = new MissionWeapon(hit.item, null, null);
                weapon.CurrentUsageIndex = hit.index;
                var frame = target.Object.GameEntity.GetGlobalFrame();
                destruction.TriggerOnHit(null, Math.Max(1, (int)Math.Ceiling(destruction.HitPoint)), frame.origin,
                    frame.rotation.f, in weapon, -1, null);
            }
            string output = "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new { missionObjectId = target.Object.Id.Id,
                before, after = destruction._currentStateIndex, hitPoints = destruction.HitPoint, destroyed = destruction.IsDestroyed });
            return destruction.IsDestroyed ? new CoopCommandResult(true, output)
                : new CoopCommandResult(false, output, "hit_rejected");
        }
    }

    public sealed class StuckMissilesCoopCommand : ICoopCommand
    {
        private readonly INetworkWorldItemRegistry worldItemRegistry;
        public StuckMissilesCoopCommand(INetworkWorldItemRegistry worldItemRegistry) => this.worldItemRegistry = worldItemRegistry;
        public string Prefix => "coop.debug.missile";
        public string Name => "stuck";
        public string Description => "Lists stuck missiles by network id with their global frames and parent names.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } =
        {
            new ExpectedArgs("offset", "Row offset 0..100000, default 0.", false),
            new ExpectedArgs("limit", "Rows 1..64, default 16.", false)
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            int offset = 0, limit = 16;
            if (args.Count > 2
                || (args.Count > 0 && !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out offset))
                || (args.Count > 1 && !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out limit))
                || offset > 100000 || limit < 1 || limit > 64)
                return new CoopCommandResult(false, "Expected offset 0..100000 and limit 1..64.", "invalid_arguments");
            if (ModInformation.IsServer || Mission.Current == null)
                return new CoopCommandResult(false, "An active client mission is required.", "no_mission");
            var stuck = worldItemRegistry.GetAll().Where(entry => entry.Value != null && !entry.Value.IsRemoved
                && entry.Value.GameEntity.IsValid && entry.Value.IsStuckMissile()).OrderBy(entry => entry.Key).ToArray();
            var output = new StringBuilder();
            output.Append("STUCK_MISSILES total=").Append(stuck.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" offset=").Append(offset.ToString(CultureInfo.InvariantCulture));
            foreach (var entry in stuck.Skip(offset).Take(limit))
            {
                var frame = entry.Value.GameEntity.GetGlobalFrame();
                Vec3 o = frame.origin, s = frame.rotation.s, f = frame.rotation.f, u = frame.rotation.u;
                var parent = entry.Value.GameEntity.Parent;
                output.Append('\n').Append(string.Format(CultureInfo.InvariantCulture,
                    "STUCK_MISSILE worldItem={0} item={1} amount={2} o={3:R},{4:R},{5:R} s={6:R},{7:R},{8:R} " +
                    "f={9:R},{10:R},{11:R} u={12:R},{13:R},{14:R} parent={15}", entry.Key,
                    entry.Value.WeaponCopy.Item?.StringId, entry.Value.WeaponCopy.Amount, o.x, o.y, o.z,
                    s.x, s.y, s.z, f.x, f.y, f.z, u.x, u.y, u.z, parent.IsValid ? parent.Name : "none"));
            }
            return new CoopCommandResult(true, output.ToString());
        }
    }
}
#endif
