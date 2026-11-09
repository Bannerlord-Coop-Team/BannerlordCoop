using Common;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Handlers;
using Missions.Missiles.Message;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Handlers;

public partial class MissileHandler
{
    private const int MaxTrackedOutcomes = 8192;
    private const float RotationRoundTripToleranceSquared = 4e-12f;
    private static readonly TimeSpan PendingOutcomeLifetime = TimeSpan.FromSeconds(30);
    private readonly IWeaponDropWorldItemSpawner worldItemSpawner;
    private readonly Dictionary<Guid, ReplayOutcome> replayOutcomes = new();
    private readonly Queue<Guid> replayOutcomeOrder = new();
    private readonly Dictionary<Guid, (NetworkMissileOutcome Message, DateTime Expires)> pendingOutcomes = new();
    private readonly Dictionary<SpawnedItemEntity, Guid> bouncingItems = new();
    private long sentOutcomes;
    private long skippedOutcomes;

    private sealed class ReplayOutcome
    {
        public MissionWeapon Weapon { get; }
        public Mission.Missile Missile { get; set; }
        public bool Applied { get; set; }

        public ReplayOutcome(MissionWeapon weapon)
        {
            Weapon = weapon;
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
        MatrixFrame frame, bool isAttachedFrameLocal, Agent victim, bool shield, sbyte boneIndex)
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
                victimId, boneIndex, isAttachedFrameLocal,
                shield ? (int)victim.GetOffhandWieldedItemIndex() : 0, 0f);
            return;
        }

        SpawnedItemEntity item = missile.Entity?.GetFirstScriptOfType<SpawnedItemEntity>();
        if (item == null) return;

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
            SendOutcome(worldItemId, MissileOutcomeKind.Ground, globalFrame, null, 0, false, 0, lifeTime);
        }
    }

    private void HandlePhysicsSettled(MessagePayload<MissilePhysicsSettled> payload)
    {
        SpawnedItemEntity item = payload.What.Item;
        if (disposed || !bouncingItems.TryGetValue(item, out Guid itemId)) return;

        bouncingItems.Remove(item);
        if (worldItemSpawner.TryGetState(item, out var frame, out float lifeTime))
            SendOutcome(itemId, MissileOutcomeKind.BounceSettled, frame, null, 0, false, 0, lifeTime);
    }

    private void SendOutcome(Guid itemId, MissileOutcomeKind kind, MatrixFrame frame, Guid? victimId,
        int boneIndex, bool isAttachedFrameLocal, int shieldSlot, float remainingLifeTime)
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
            victimId, boneIndex, isAttachedFrameLocal, shieldSlot, remainingLifeTime));
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
            if (worldItemSpawner.IsReady && replayOutcomes.TryGetValue(message.WorldItemId, out var replay))
                ApplyOutcome(message, replay);
            else if (pendingOutcomes.Count < MaxTrackedOutcomes)
                pendingOutcomes[message.WorldItemId] = (message, DateTime.UtcNow.Add(PendingOutcomeLifetime));
            else
                Logger.Warning("Dropping missile outcome: {Count} shots are pending", pendingOutcomes.Count);
        });
    }

    private void ApplyOutcome(NetworkMissileOutcome message, ReplayOutcome replay)
    {
        if (replay.Applied) return;
        replay.Applied = true;

        float[] rotation = message.Rotation;
        if (rotation == null || (rotation.Length != 4 && rotation.Length != 9))
        {
            Logger.Warning("Missing rotation for missile outcome {WorldItemId}", message.WorldItemId);
            return;
        }
        Mat3 orientation = rotation.Length == 4
            ? new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]).ToMat3()
            : new Mat3(new Vec3(rotation[0], rotation[1], rotation[2]),
                new Vec3(rotation[3], rotation[4], rotation[5]), new Vec3(rotation[6], rotation[7], rotation[8]));
        var frame = new MatrixFrame(orientation, message.Origin);

        Mission mission = Mission.Current;
        if (mission == null) return;

        using var allowed = new AllowedThread();
        Mission.Missile missile = replay.Missile;
        replay.Missile = null;
        if (missile != null && mission._missilesDictionary.TryGetValue(missile.Index, out var flying)
            && ReferenceEquals(flying, missile))
        {
            missile.Entity.Remove(81);
            mission.RemoveMissileAsClient(missile.Index);
        }

        MissionWeapon weapon = replay.Weapon;
        if (message.Kind == MissileOutcomeKind.Body || message.Kind == MissileOutcomeKind.Shield)
        {
            if (!message.VictimId.HasValue
                || !networkAgentRegistry.TryGetCosmeticAgent(message.VictimId.Value, out var victim)
                || victim == null || victim.Mission != mission)
            {
                Logger.Warning("Failed to get Agent using {AgentId}", message.VictimId);
                return;
            }

            if (message.Kind == MissileOutcomeKind.Shield)
                victim.AttachWeaponToWeapon((EquipmentIndex)message.ShieldSlot, weapon, null, ref frame);
            else
                victim.AttachWeaponToBone(weapon, null, (sbyte)message.BoneIndex, ref frame);
            return;
        }

        bool settledBounce = message.Kind == MissileOutcomeKind.BounceSettled;
        Mission.WeaponSpawnFlags flags = Mission.WeaponSpawnFlags.AsMissile
            | (settledBounce ? Mission.WeaponSpawnFlags.WithPhysics : Mission.WeaponSpawnFlags.WithStaticPhysics);
        if (!worldItemSpawner.TrySpawn(ref weapon, flags, true, message.RemainingLifeTime, frame, out var item))
        {
            Logger.Warning("Failed to spawn missile world item {WorldItemId}", message.WorldItemId);
            return;
        }

        // Co-op does not tick vanilla's native multiplayer frame fade, so spawn at the settled frame first.
        if (settledBounce)
            item.StopPhysicsAndSetFrameForClient(frame, null);
        messageBroker.Publish(this, new MissileItemIdentified(item, message.WorldItemId));
    }

    private void PruneOutcomes()
    {
        if (disposed) return;

        List<Guid> expired = null;
        DateTime now = DateTime.UtcNow;
        foreach (var pending in pendingOutcomes)
        {
            if (pending.Value.Expires <= now
                || (worldItemSpawner.IsReady && replayOutcomes.ContainsKey(pending.Key)))
                (expired ??= new List<Guid>()).Add(pending.Key);
        }
        if (expired != null)
        {
            foreach (Guid itemId in expired)
            {
                var pending = pendingOutcomes[itemId];
                pendingOutcomes.Remove(itemId);
                if (pending.Expires <= now)
                    Logger.Warning("Failed to get missile shot using {WorldItemId}", itemId);
                else
                    GameThread.RunSafe(() => ApplyOutcome(pending.Message, replayOutcomes[itemId]));
            }
        }

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
