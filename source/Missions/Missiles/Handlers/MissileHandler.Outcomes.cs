using Common;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Handlers;
using Missions.Agents;
using Missions.Battles;
using Missions.Missiles.Message;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Handlers;

public partial class MissileHandler
{
    private const int MaxTrackedOutcomes = 8192;
    private const float RotationRoundTripToleranceSquared = 4e-12f;
    private static readonly TimeSpan PendingOutcomeLifetime = TimeSpan.FromSeconds(30);
    private readonly IWeaponDropWorldItemSpawner worldItemSpawner;
    private readonly INetworkWorldItemRegistry worldItemRegistry;
    private readonly INavalShipEngine shipEngine;
    private readonly IMissionObjectPath objectPath;
    private readonly Dictionary<Guid, ReplayOutcome> replayOutcomes = new();
    // Completed identities suppress duplicates without entering the per-frame retry and expiry scan.
    private readonly HashSet<Guid> completedOutcomes = new();
    private readonly Queue<Guid> completedOutcomeOrder = new();
    private readonly Dictionary<Guid, (NetworkMissileOutcome Message, DateTime Expires)> pendingOutcomes = new();
    private readonly Dictionary<SpawnedItemEntity, Guid> bouncingItems = new();
    private long sentOutcomes;
    private long skippedOutcomes;

    private sealed class ReplayOutcome
    {
        public MissionWeapon Weapon { get; }
        public Mission.Missile Missile { get; set; }
        public NetworkMissileOutcome Message { get; set; }
        public bool Applied { get; set; }
        public DateTime Expires { get; set; }

        public ReplayOutcome(MissionWeapon weapon)
        {
            Weapon = weapon;
            Expires = DateTime.UtcNow.Add(PendingOutcomeLifetime);
        }
    }

    public Guid PrepareCollision(Mission.Missile missile, ref Mission.MissileCollisionReaction reaction)
    {
        if (disposed || reaction == Mission.MissileCollisionReaction.PassThrough) return Guid.Empty;

        if (reconstructedShots.TryGetValue(missile.Index, out var replay)
            && ReferenceEquals(replay.Missile, missile))
        {
            reaction = Mission.MissileCollisionReaction.BecomeInvisible;
            return Guid.Empty;
        }

        return localShots.TryGetValue(missile.Index, out var local)
            ? CreateMissileItemId(local.AgentId, local.ShotSequence)
            : Guid.Empty;
    }

    public void ReportCollision(Guid worldItemId, Mission.Missile missile, Mission.MissileCollisionReaction reaction,
        MatrixFrame frame, Agent victim, bool shield, sbyte boneIndex, MissionObject parent)
    {
        if (disposed || worldItemId == Guid.Empty) return;

        if (reaction == Mission.MissileCollisionReaction.BecomeInvisible)
        {
            skippedOutcomes++;
            Logger.Debug("[MissileOutcome] sent={Sent} skipped={Skipped}", sentOutcomes, skippedOutcomes);
            return;
        }

        if (reaction == Mission.MissileCollisionReaction.Stick && victim != null)
        {
            if (!networkAgentRegistry.TryGetCosmeticAgentId(victim, out Guid victimId))
            {
                Logger.Warning("Failed to get Agent id for missile attachment to {Agent}", victim);
                return;
            }

            SendOutcome(worldItemId, shield ? MissileOutcomeKind.Shield : MissileOutcomeKind.Body, frame,
                victimId, shield ? 0 : boneIndex,
                shield ? (int)victim.GetOffhandWieldedItemIndex() : 0, 0f);
            return;
        }

        SpawnedItemEntity item = missile.Entity?.GetFirstScriptOfType<SpawnedItemEntity>();
        if (item == null) return;

        // The shot still names its item even if native deletion already retired the shooter identity.
        if (!worldItemRegistry.TryGetId(item, out Guid registeredId) || registeredId != worldItemId)
            messageBroker.Publish(this, new MissileItemIdentified(item, worldItemId));

        if (reaction == Mission.MissileCollisionReaction.BounceBack)
        {
            if (bouncingItems.Count < MaxTrackedOutcomes)
                bouncingItems[item] = worldItemId;
            else
                Logger.Warning("Dropping missile settle tracking: {Count} outcomes are pending", bouncingItems.Count);
        }
        else if (reaction == Mission.MissileCollisionReaction.Stick
            && worldItemSpawner.TryGetState(item, out var globalFrame, out float lifeTime))
        {
            SendWorldOutcome(worldItemId, MissileOutcomeKind.Ground, globalFrame, lifeTime, parent, item.GameEntity.Parent);
        }
    }

    private void HandlePhysicsSettled(MessagePayload<MissilePhysicsSettled> payload)
    {
        SpawnedItemEntity item = payload.What.Item;
        if (disposed || !bouncingItems.TryGetValue(item, out Guid itemId)) return;

        bouncingItems.Remove(item);
        if (!worldItemSpawner.TryGetState(item, out var frame, out float lifeTime)) return;

        MissionObject parent = null;
        if (item._groundEntityWhenDisabled != null)
        {
            foreach (MissionObject candidate in Mission.Current.MissionObjects)
            {
                if (candidate.GameEntity != item._groundEntityWhenDisabled.WeakEntity) continue;
                parent = candidate;
                break;
            }
            if (parent == null)
            {
                Logger.Warning("Failed to get mission object for settled missile parent {WorldItemId}", itemId);
                return;
            }
        }
        SendWorldOutcome(itemId, MissileOutcomeKind.BounceSettled, frame, lifeTime, parent);
    }

    private void SendWorldOutcome(Guid itemId, MissileOutcomeKind kind, MatrixFrame frame,
        float remainingLifeTime, MissionObject parent, WeakGameEntity attachmentParent = default)
    {
        int? sceneId = null;
        Guid? worldItemId = null;
        Guid? shipId = null;
        string shipObjectKey = null;
        int? shipObjectIndex = null;
        string parentEntityPath = null;
        if (parent != null)
        {
            if (!parent.Id.CreatedAtRuntime)
                sceneId = parent.Id.Id;
            else if (parent is SpawnedItemEntity item && worldItemRegistry.TryGetId(item, out Guid parentId))
                worldItemId = parentId;
            else
            {
                // The component owns the actual mission's transient ship registry.
                var ships = Mission.Current.GetMissionBehavior<CoopMissionController>()?.MissionComponent.ShipRegistry;
                if (ships != null && ships.TryGetByHull(parent, out var ship))
                    shipId = ship.ShipId;
                else if (ships != null && shipEngine != null)
                {
                    foreach (var candidate in ships.Ships)
                    {
                        if (candidate.Hull == null || !shipEngine.TryDescribeObject(candidate.Hull, parent,
                            out shipObjectKey, out int objectIndex)) continue;
                        shipObjectIndex = objectIndex;
                        shipId = candidate.ShipId;
                        break;
                    }
                }
                if (!shipId.HasValue)
                {
                    Logger.Warning("Failed to get network identity for missile parent {ParentId}", parent.Id);
                    return;
                }
            }
            if (kind == MissileOutcomeKind.BounceSettled) attachmentParent = parent.GameEntity;
            if (attachmentParent != parent.GameEntity)
            {
                parentEntityPath = objectPath.Describe(attachmentParent, parent.GameEntity);
                if (parentEntityPath == null)
                {
                    Logger.Warning("Failed to get entity path for missile parent {ParentId}", parent.Id);
                    return;
                }
            }
            frame = attachmentParent.GetGlobalFrame().TransformToLocalNonOrthogonal(in frame);
        }
        SendOutcome(itemId, kind, frame, null, 0, 0, remainingLifeTime, sceneId, worldItemId, shipId,
            shipObjectKey, shipObjectIndex, parentEntityPath);
    }

    private void SendOutcome(Guid itemId, MissileOutcomeKind kind, MatrixFrame frame, Guid? victimId,
        int boneIndex, int shieldSlot, float remainingLifeTime,
        int? parentMissionObjectId = null, Guid? parentWorldItemId = null, Guid? parentShipId = null,
        string parentShipObjectKey = null, int? parentShipObjectIndex = null, string parentEntityPath = null)
    {
        Mat3 rotation = frame.rotation;
        Quaternion quaternion = Quaternion.QuaternionFromMat3(rotation);
        Mat3 decoded = quaternion.ToMat3();
        bool exact = victimId.HasValue
            || (rotation.s - decoded.s).LengthSquared > RotationRoundTripToleranceSquared
            || (rotation.f - decoded.f).LengthSquared > RotationRoundTripToleranceSquared
            || (rotation.u - decoded.u).LengthSquared > RotationRoundTripToleranceSquared;
        float[] orientation = exact
            ? new[] { rotation.s.x, rotation.s.y, rotation.s.z, rotation.f.x, rotation.f.y, rotation.f.z,
                rotation.u.x, rotation.u.y, rotation.u.z }
            : new[] { quaternion.X, quaternion.Y, quaternion.Z, quaternion.W };

        network.SendAll(new NetworkMissileOutcome(itemId, kind, frame.origin, orientation,
            victimId, boneIndex, shieldSlot, remainingLifeTime, parentMissionObjectId, parentWorldItemId, parentShipId,
            parentShipObjectKey, parentShipObjectIndex, parentEntityPath));
        sentOutcomes++;
        Logger.Debug("[MissileOutcome] sent={Sent} skipped={Skipped} kind={Kind}",
            sentOutcomes, skippedOutcomes, kind);
    }

    private void ReceiveOutcome(MessagePayload<NetworkMissileOutcome> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (disposed) return;

            NetworkMissileOutcome message = payload.What;
            if (completedOutcomes.Contains(message.WorldItemId)) return;
            if (replayOutcomes.TryGetValue(message.WorldItemId, out var replay))
            {
                if (replay.Message == null)
                {
                    replay.Message = message;
                    replay.Expires = DateTime.UtcNow.Add(PendingOutcomeLifetime);
                }
                if (replay.Expires > DateTime.UtcNow && TryApplyOutcome(replay.Message, replay))
                    replayOutcomes.Remove(message.WorldItemId);
                return;
            }
            if (pendingOutcomes.ContainsKey(message.WorldItemId)) return;
            // Only outcomes whose shots have not arrived need a separate bounded slot.
            if (pendingOutcomes.Count >= MaxTrackedOutcomes)
            {
                Logger.Warning("Dropping missile outcome: {Count} shots are pending", pendingOutcomes.Count);
                return;
            }
            pendingOutcomes.Add(message.WorldItemId, (message, DateTime.UtcNow.Add(PendingOutcomeLifetime)));
        });
    }

    private bool TryTrackOutcome(Guid itemId, MissionWeapon weapon, out ReplayOutcome replay)
    {
        if (replayOutcomes.TryGetValue(itemId, out replay)) return true;
        while (replayOutcomes.Count + completedOutcomes.Count >= MaxTrackedOutcomes && completedOutcomeOrder.Count > 0)
            completedOutcomes.Remove(completedOutcomeOrder.Dequeue());
        if (replayOutcomes.Count >= MaxTrackedOutcomes) return false;

        replay = new ReplayOutcome(weapon);
        if (pendingOutcomes.TryGetValue(itemId, out var pending))
        {
            replay.Message = pending.Message;
            replay.Expires = pending.Expires;
            pendingOutcomes.Remove(itemId);
        }
        replayOutcomes.Add(itemId, replay);
        return true;
    }

    private bool TryApplyOutcome(NetworkMissileOutcome message, ReplayOutcome replay)
    {
        if (replay.Applied) return true;
        Mission mission = Mission.Current;
        if (mission == null || !worldItemSpawner.IsReady) return false;

        float[] rotation = message.Rotation;
        if (rotation == null || (rotation.Length != 4 && rotation.Length != 9))
            return false;
        Mat3 orientation = rotation.Length == 4
            ? new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]).ToMat3()
            : new Mat3(new Vec3(rotation[0], rotation[1], rotation[2]),
                new Vec3(rotation[3], rotation[4], rotation[5]), new Vec3(rotation[6], rotation[7], rotation[8]));
        var frame = new MatrixFrame(orientation, message.Origin);

        using var allowed = new AllowedThread();
        MissionWeapon weapon = replay.Weapon;
        if (message.Kind == MissileOutcomeKind.Body || message.Kind == MissileOutcomeKind.Shield)
        {
            if (!message.VictimId.HasValue
                || !networkAgentRegistry.TryGetCosmeticAgent(message.VictimId.Value, out var victim)
                || victim == null || victim.Mission != mission)
                return false;

            if (message.Kind == MissileOutcomeKind.Shield)
                victim.AttachWeaponToWeapon((EquipmentIndex)message.ShieldSlot, weapon, null, ref frame);
            else
                victim.AttachWeaponToBone(weapon, null, (sbyte)message.BoneIndex, ref frame);
        }
        else
        {
            if (!TryResolveParent(message, mission, out var parent)) return false;
            GameEntity attachmentParent = null;
            if (parent != null)
            {
                var entity = message.ParentEntityPath == null ? parent.GameEntity
                    : objectPath.Resolve(parent.GameEntity, message.ParentEntityPath);
                if (!entity.IsValid) return false;
                attachmentParent = GameEntity.CreateFromWeakEntity(entity);
            }

            bool settledBounce = message.Kind == MissileOutcomeKind.BounceSettled;
            Mission.WeaponSpawnFlags flags = Mission.WeaponSpawnFlags.AsMissile
                | (settledBounce ? Mission.WeaponSpawnFlags.WithPhysics : Mission.WeaponSpawnFlags.WithStaticPhysics);
            if (!worldItemSpawner.TrySpawn(ref weapon, flags, true, message.RemainingLifeTime, frame, out var item, parent,
                settledBounce ? null : attachmentParent))
                return false;

            // Spawn at the final frame because co-op does not tick vanilla's multiplayer frame fade.
            if (settledBounce)
                item.StopPhysicsAndSetFrameForClient(frame, attachmentParent);
            messageBroker.Publish(this, new MissileItemIdentified(item, message.WorldItemId));
        }

        replay.Applied = true;
        completedOutcomes.Add(message.WorldItemId);
        completedOutcomeOrder.Enqueue(message.WorldItemId);
        Mission.Missile missile = replay.Missile;
        replay.Missile = null;
        if (missile != null && mission._missilesDictionary.TryGetValue(missile.Index, out var flying)
            && ReferenceEquals(flying, missile))
        {
            missile.Entity.Remove(81);
            mission.RemoveMissileAsClient(missile.Index);
        }
        return true;
    }

    private bool TryResolveParent(NetworkMissileOutcome message, Mission mission, out MissionObject parent)
    {
        parent = null;
        if (message.ParentMissionObjectId.HasValue)
        {
            foreach (MissionObject candidate in mission.MissionObjects)
            {
                if (candidate.Id.CreatedAtRuntime || candidate.Id.Id != message.ParentMissionObjectId.Value) continue;
                parent = candidate;
                break;
            }
            return parent != null;
        }
        if (message.ParentWorldItemId.HasValue)
        {
            if (!worldItemRegistry.TryGet(message.ParentWorldItemId.Value, out var item)
                || !worldItemSpawner.IsPresent(item)) return false;
            parent = item;
        }
        if (message.ParentShipId.HasValue)
        {
            var ships = mission.GetMissionBehavior<CoopMissionController>()?.MissionComponent.ShipRegistry;
            if (ships == null || !ships.TryGet(message.ParentShipId.Value, out var ship) || ship.Hull == null)
                return false;
            parent = message.ParentShipObjectIndex.HasValue
                ? shipEngine?.ResolveObject(ship.Hull, message.ParentShipObjectKey ?? string.Empty, message.ParentShipObjectIndex.Value)
                : ship.Hull;
            return parent != null;
        }
        return true;
    }

    private void PruneOutcomes(DateTime now)
    {
        if (disposed) return;

        List<Guid> finished = null;
        foreach (var pending in pendingOutcomes)
        {
            if (pending.Value.Expires <= now)
            {
                Logger.Warning("Failed to get missile shot using {WorldItemId}", pending.Key);
                (finished ??= new List<Guid>()).Add(pending.Key);
            }
        }
        if (finished != null)
            foreach (Guid itemId in finished) pendingOutcomes.Remove(itemId);

        List<Guid> expired = null;
        foreach (var replay in replayOutcomes)
        {
            if (replay.Value.Expires <= now)
            {
                if (replay.Value.Message != null)
                    Logger.Warning("Failed to apply missile outcome using {WorldItemId}: victim={VictimId} parent={ParentId}",
                        replay.Key, replay.Value.Message.VictimId, replay.Value.Message.ParentMissionObjectId);
                (expired ??= new List<Guid>()).Add(replay.Key);
            }
            else if (replay.Value.Message != null && TryApplyOutcome(replay.Value.Message, replay.Value))
                (expired ??= new List<Guid>()).Add(replay.Key);
        }
        if (expired != null)
            foreach (Guid itemId in expired) replayOutcomes.Remove(itemId);

        List<SpawnedItemEntity> removed = null;
        foreach (var bounce in bouncingItems)
        {
            if (!worldItemSpawner.IsPresent(bounce.Key))
                (removed ??= new List<SpawnedItemEntity>()).Add(bounce.Key);
        }
        if (removed != null)
        {
            foreach (SpawnedItemEntity item in removed)
                bouncingItems.Remove(item);
        }
    }
}
