using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions.Missiles.Message;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Missiles.Handlers;

/// <summary>Synchronizes missiles fired by registered mission agents.</summary>
public interface IMissileHandler : IHandler
{
    void DrainPendingShots();
    bool IsReconstructionPending(Guid agentId, long shotSequence);
    bool TryTakeLocalShot(int missileIndex, out Guid agentId, out long shotSequence);
    void RecordImpactHint(Guid attackerAgentId, long shotSequence, Guid victimAgentId,
        bool isMountFallback, Vec3 impactVelocity);
}

/// <inheritdoc/>
public class MissileHandler : IMissileHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissileHandler>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry networkAgentRegistry;
    private readonly ConcurrentQueue<(NetworkAgentShoot Shot, int Attempts)> pendingShots = new();
    private readonly ConcurrentDictionary<(Guid AgentId, long ShotSequence), byte> pendingReconstructions = new();
    private readonly ConcurrentDictionary<(Guid AgentId, long ShotSequence), ImpactHint> impactHints = new();
    private readonly Dictionary<int, (Guid AgentId, long ShotSequence)> localShots = new();
    private long nextShotSequence = CreateShotSequenceSeed();
    private volatile bool disposed;
    private const int MaxReconstructionAttempts = 30;

    private readonly struct ImpactHint
    {
        public Guid VictimAgentId { get; }
        public bool IsMountFallback { get; }
        public Vec3 ImpactVelocity { get; }

        public ImpactHint(Guid victimAgentId, bool isMountFallback, Vec3 impactVelocity)
        {
            VictimAgentId = victimAgentId;
            IsMountFallback = isMountFallback;
            ImpactVelocity = impactVelocity;
        }
    }

    public MissileHandler(IBattleNetwork network, IMessageBroker messageBroker,
        INetworkAgentRegistry networkAgentRegistry)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.networkAgentRegistry = networkAgentRegistry;
        messageBroker.Subscribe<AgentShoot>(AgentShootSend);
        messageBroker.Subscribe<NetworkAgentShoot>(AgentShootReceive);
    }

    ~MissileHandler() => Dispose();

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        messageBroker.Unsubscribe<AgentShoot>(AgentShootSend);
        messageBroker.Unsubscribe<NetworkAgentShoot>(AgentShootReceive);
        pendingReconstructions.Clear();
        impactHints.Clear();
        localShots.Clear();
    }

    private void AgentShootSend(MessagePayload<AgentShoot> payload)
    {
        if (!networkAgentRegistry.IsLocallyControlled(payload.What.Agent))
            return;

        if (!networkAgentRegistry.TryGetAgentInfo(payload.What.Agent, out var agentInfo))
        {
            Logger.Warning("No agentID was found for the Agent: {agent}", payload.What.Agent);
            return;
        }

        MissionWeapon missileWeapon = payload.What.MissionWeapon.CurrentUsageItem.IsRangedWeapon
            && payload.What.MissionWeapon.CurrentUsageItem.IsConsumable
                ? payload.What.MissionWeapon
                : payload.What.MissionWeapon.AmmoWeapon;
        ItemObject missileItem = missileWeapon.Item;
        if (missileItem == null || string.IsNullOrEmpty(missileItem.StringId))
        {
            Logger.Warning("Cannot send missile {idx}: its item has no StringId", payload.What.MissileIndex);
            return;
        }

        if (missileWeapon.IsEmpty || missileWeapon.CurrentUsageIndex < 0
            || missileWeapon.CurrentUsageIndex >= missileWeapon.WeaponsCount)
        {
            Logger.Warning("Cannot send missile {idx}: item {itemId} has invalid usage {usageIndex}/{usageCount}",
                payload.What.MissileIndex, missileItem.StringId, missileWeapon.CurrentUsageIndex,
                missileWeapon.WeaponsCount);
            return;
        }

        string modifierId = missileWeapon.ItemModifier?.StringId;
        long sequence = Interlocked.Increment(ref nextShotSequence);
        localShots[payload.What.MissileIndex] = (agentInfo.AgentId, sequence);

        Logger.Debug("Sending missile sequence {sequence}, source {index}, item {itemId}, usage {usageIndex}/{usageCount}",
            sequence, payload.What.MissileIndex, missileItem.StringId, missileWeapon.CurrentUsageIndex,
            missileWeapon.WeaponsCount);
        network.SendAll(new NetworkAgentShoot(agentInfo.AgentId, payload.What.Position, payload.What.Direction,
            payload.What.Orientation, payload.What.HasRigidBody, missileItem.StringId, modifierId,
            missileWeapon.Banner, payload.What.MissileIndex, payload.What.BaseSpeed, payload.What.Speed,
            missileWeapon.CurrentUsageIndex, sequence));
    }

    private void AgentShootReceive(MessagePayload<NetworkAgentShoot> payload)
    {
        NetworkAgentShoot shot = payload.What;
        var key = (shot.AgentId, shot.ShotSequence);
        if (disposed || shot.ShotSequence == 0 || !pendingReconstructions.TryAdd(key, 0))
            return;

        pendingShots.Enqueue((shot, 0));
    }

    public void DrainPendingShots()
    {
        int count = pendingShots.Count;
        for (int i = 0; i < count && pendingShots.TryDequeue(out var pending); i++)
        {
            bool retry = false;
            try
            {
                if (!disposed)
                    retry = !Reconstruct(pending.Shot);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to reconstruct missile {MissileIndex}", pending.Shot.MissileIndex);
            }

            if (retry && pending.Attempts < MaxReconstructionAttempts)
            {
                pendingShots.Enqueue((pending.Shot, pending.Attempts + 1));
                continue;
            }

            if (retry)
            {
                Logger.Warning("Dropping missile {MissileIndex}: no cosmetic shooter became available",
                    pending.Shot.MissileIndex);
            }
            CompleteReconstruction(pending.Shot);
        }
    }

    public bool IsReconstructionPending(Guid agentId, long shotSequence) =>
        !disposed && shotSequence != 0 && pendingReconstructions.ContainsKey((agentId, shotSequence));

    public bool TryTakeLocalShot(int missileIndex, out Guid agentId, out long shotSequence)
    {
        if (!disposed && localShots.TryGetValue(missileIndex, out var shot))
        {
            localShots.Remove(missileIndex);
            agentId = shot.AgentId;
            shotSequence = shot.ShotSequence;
            return true;
        }

        agentId = Guid.Empty;
        shotSequence = 0;
        return false;
    }

    public void RecordImpactHint(Guid attackerAgentId, long shotSequence, Guid victimAgentId,
        bool isMountFallback, Vec3 impactVelocity)
    {
        var key = (attackerAgentId, shotSequence);
        if (disposed || attackerAgentId == Guid.Empty || shotSequence == 0
            || !pendingReconstructions.ContainsKey(key))
        {
            return;
        }

        impactHints.TryAdd(key, new ImpactHint(victimAgentId, isMountFallback, impactVelocity));
        if (!pendingReconstructions.ContainsKey(key))
            impactHints.TryRemove(key, out _);
    }

    private void CompleteReconstruction(NetworkAgentShoot shot)
    {
        var key = (shot.AgentId, shot.ShotSequence);
        pendingReconstructions.TryRemove(key, out _);
        impactHints.TryRemove(key, out _);
    }

    private bool TryTakeImpactHint(NetworkAgentShoot shot, out ImpactHint hint) =>
        impactHints.TryRemove((shot.AgentId, shot.ShotSequence), out hint);

    private bool Reconstruct(NetworkAgentShoot shot)
    {
        Mission mission = Mission.Current;
        if (mission == null)
            return false;

        Agent agent = null;
        if (networkAgentRegistry.TryGetAgentInfo(shot.AgentId, out var info)
            && info.Agent != null && info.Agent.Mission == mission && info.Agent.IsActive()
            && !networkAgentRegistry.IsLocallyControlled(info.Agent))
        {
            agent = info.Agent;
        }
        else if (BattleSpawnGate.IsCoopBattleActive)
        {
            agent = FindStandInShooter(mission);
        }

        if (agent == null)
            return false;

        ItemObject missileItem = string.IsNullOrEmpty(shot.MissileItemId)
            ? null
            : MBObjectManager.Instance.GetObject<ItemObject>(shot.MissileItemId);
        if (missileItem == null)
        {
            Logger.Warning("Cannot reconstruct missile {index}: item {itemId} was not found",
                shot.MissileIndex, shot.MissileItemId);
            return true;
        }

        ItemModifier modifier = string.IsNullOrEmpty(shot.ItemModifierId)
            ? null
            : MBObjectManager.Instance.GetObject<ItemModifier>(shot.ItemModifierId);
        MissionWeapon missileWeapon = new MissionWeapon(missileItem, modifier, shot.Banner);
        if (missileWeapon.IsEmpty || shot.CurrentUsageIndex < 0
            || shot.CurrentUsageIndex >= missileWeapon.WeaponsCount)
        {
            Logger.Warning("Cannot reconstruct missile {index}: item {itemId} has invalid usage {usageIndex}/{usageCount}",
                shot.MissileIndex, shot.MissileItemId, shot.CurrentUsageIndex, missileWeapon.WeaponsCount);
            return true;
        }

        missileWeapon.CurrentUsageIndex = shot.CurrentUsageIndex;
        missileWeapon.Amount = 1;

        ImpactHint hint = default;
        Vec3 impactTarget = default;
        bool fastForward = TryTakeImpactHint(shot, out hint)
            && TryResolveImpactTarget(hint, mission, out impactTarget);
        MissileReplayPlan replay = MissileReplayPlanner.Plan(shot.Position, shot.Velocity, shot.Orientation,
            shot.Speed, fastForward, impactTarget, hint.ImpactVelocity);
        if (replay.IsFastForwarded)
        {
            Logger.Debug("Fast-forwarding missile sequence {sequence} to a {durationMs:0}ms segment aimed at the current victim position",
                shot.ShotSequence, replay.RemainingFlightSeconds * 1000f);
        }

        Vec3 position = replay.Position;
        Vec3 direction = replay.Direction;
        Mat3 orientation = replay.Orientation;
        float baseSpeed = IsUsableSpeed(shot.BaseSpeed) ? shot.BaseSpeed : replay.Speed;
        WeaponData weaponData = missileWeapon.GetWeaponData(true);
        int index;
        GameEntity missileEntity;
        try
        {
            if (missileWeapon.WeaponsCount == 1)
            {
                WeaponStatsData stats = missileWeapon.GetWeaponStatsDataForUsage(0);
                index = mission.AddMissileSingleUsageAux(-1, false, agent, in weaponData, in stats, 0f,
                    ref position, ref direction, ref orientation, baseSpeed, replay.Speed, shot.HasRigidBody,
                    WeakGameEntity.Invalid, false, out missileEntity);
            }
            else
            {
                WeaponStatsData[] stats = missileWeapon.GetWeaponStatsData();
                index = mission.AddMissileAux(-1, false, agent, in weaponData, stats, 0f,
                    ref position, ref direction, ref orientation, baseSpeed, replay.Speed, shot.HasRigidBody,
                    WeakGameEntity.Invalid, false, out missileEntity);
            }
        }
        finally
        {
            weaponData.DeinitializeManagedPointers();
        }

        if (index < 0 || missileEntity == null || !missileEntity.WeakEntity.IsValid)
        {
            Logger.Warning("Cannot reconstruct missile {index}: native add returned no entity", shot.MissileIndex);
            return true;
        }

        Mission.Missile missile = new Mission.Missile(mission, index, missileEntity, agent, missileWeapon, null);
        mission._missilesList.Add(missile);
        mission._missilesDictionary[index] = missile;

        Logger.Debug("Reconstructed missile sequence {sequence}, source {sourceIndex}, as local {localIndex}",
            shot.ShotSequence, shot.MissileIndex, index);
        messageBroker.Publish(this, new MissileReconstructed(
            shot.AgentId,
            shot.ShotSequence,
            shot.MissileItemId,
            position,
            replay.Speed,
            replay.RemainingFlightSeconds));
        return true;
    }

    private bool TryResolveImpactTarget(ImpactHint hint, Mission mission, out Vec3 target)
    {
        if (networkAgentRegistry.TryGetAgentInfo(hint.VictimAgentId, out var info))
        {
            Agent victim = hint.IsMountFallback ? info.Agent?.MountAgent : info.Agent;
            if (victim != null && victim.Mission == mission && victim.IsActive())
            {
                target = victim.GetChestGlobalPosition();
                return MissileReplayPlanner.IsFinite(target);
            }
        }

        target = default;
        return false;
    }

    private static Agent FindStandInShooter(Mission mission)
    {
        foreach (Agent candidate in mission.Agents)
        {
            if (candidate.IsHuman && candidate.IsActive() && candidate.Controller == AgentControllerType.None)
                return candidate;
        }

        return null;
    }

    private static bool IsUsableSpeed(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static long CreateShotSequenceSeed() =>
        BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue;
}
