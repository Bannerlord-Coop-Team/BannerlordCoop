using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions.Missiles.Message;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Missiles.Handlers;

/// <summary>
/// Handler for missiles within a co-op mission
/// </summary>
public interface IMissileHandler : IHandler
{
    void DrainPendingShots();
    bool IsReconstructionPending(Guid agentId, long shotSequence, int sourceMissileIndex);
    bool TryGetLocalShot(int sourceMissileIndex, out Guid agentId, out long shotSequence);
    void RecordImpactHint(Guid attackerAgentId, long shotSequence, Guid victimAgentId, bool isMountFallback,
        Vec3 historicalImpactPosition, Vec3 impactVelocity);
}

/// <inheritdoc/>
public class MissileHandler : IMissileHandler
{
    readonly static ILogger Logger = LogManager.GetLogger<MissileHandler>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry networkAgentRegistry;
    private readonly Queue<PendingShot> pendingShots = new();
    private readonly object outstandingGate = new();
    private readonly Dictionary<(Guid AgentId, long ShotSequence, int MissileIndex), int> outstandingReconstructions = new();
    private readonly Dictionary<(Guid AgentId, long ShotSequence), ImpactHint> impactHints = new();
    private readonly Queue<(Guid AgentId, long ShotSequence)> impactHintHistory = new();
    private readonly HashSet<(Guid AgentId, long ShotSequence)> completedShotSequences = new();
    private readonly Queue<(Guid AgentId, long ShotSequence)> completedShotHistory = new();
    private readonly Dictionary<int, LocalShot> localShots = new();
    private readonly Queue<(int MissileIndex, long ShotSequence)> localShotHistory = new();
    // A random positive seed keeps sequences distinct if authority for the same network agent moves to a
    // different client or the mission handler is recreated. The upper bound leaves effectively unlimited
    // room for Interlocked.Increment during one process lifetime.
    private long nextShotSequence = CreateShotSequenceSeed();
    private volatile bool disposed;

    private const int MaxPendingShots = 512;
    private const int MaxLocalShotMappings = 8192;
    private const int MaxImpactHints = 4096;
    private const int MaxCompletedShotSequences = 8192;
    private const double PendingShotLifetimeSeconds = 0.5;

    private enum ReconstructionResult
    {
        Reconstructed,
        Retry,
        Dropped,
    }

    private readonly struct PendingShot
    {
        public NetworkAgentShoot Shot { get; }
        public long FirstSeenTimestamp { get; }
        public string LastReason { get; }

        public PendingShot(NetworkAgentShoot shot, long firstSeenTimestamp, string lastReason)
        {
            Shot = shot;
            FirstSeenTimestamp = firstSeenTimestamp;
            LastReason = lastReason;
        }
    }

    private readonly struct LocalShot
    {
        public Guid AgentId { get; }
        public long ShotSequence { get; }

        public LocalShot(Guid agentId, long shotSequence)
        {
            AgentId = agentId;
            ShotSequence = shotSequence;
        }
    }

    private readonly struct ImpactHint
    {
        public Guid VictimAgentId { get; }
        public bool IsMountFallback { get; }
        public Vec3 HistoricalImpactPosition { get; }
        public Vec3 ImpactVelocity { get; }

        public ImpactHint(Guid victimAgentId, bool isMountFallback, Vec3 historicalImpactPosition,
            Vec3 impactVelocity)
        {
            VictimAgentId = victimAgentId;
            IsMountFallback = isMountFallback;
            HistoricalImpactPosition = historicalImpactPosition;
            ImpactVelocity = impactVelocity;
        }
    }

    public MissileHandler(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        INetworkAgentRegistry networkAgentRegistry)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.networkAgentRegistry = networkAgentRegistry;
        messageBroker.Subscribe<AgentShoot>(AgentShootSend);
        messageBroker.Subscribe<NetworkAgentShoot>(AgentShootRecieve);
    }

    ~MissileHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        messageBroker.Unsubscribe<AgentShoot>(AgentShootSend);
        messageBroker.Unsubscribe<NetworkAgentShoot>(AgentShootRecieve);
        lock (outstandingGate)
        {
            outstandingReconstructions.Clear();
            impactHints.Clear();
            impactHintHistory.Clear();
            completedShotSequences.Clear();
            completedShotHistory.Clear();
        }
        localShots.Clear();
        localShotHistory.Clear();
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

        MissionWeapon missionWeapon;

        if (payload.What.MissionWeapon.CurrentUsageItem.IsRangedWeapon &&
            payload.What.MissionWeapon.CurrentUsageItem.IsConsumable)
        {
            missionWeapon = payload.What.MissionWeapon;
        }
        else
        {
            missionWeapon = payload.What.MissionWeapon.AmmoWeapon;
        }

        ItemObject missileItem = missionWeapon.Item;
        if (missileItem == null || string.IsNullOrEmpty(missileItem.StringId))
        {
            Logger.Warning("Cannot send missile {idx}: its item has no StringId", payload.What.MissileIndex);
            return;
        }

        if (missionWeapon.IsEmpty || missionWeapon.CurrentUsageIndex < 0 ||
            missionWeapon.CurrentUsageIndex >= missionWeapon.WeaponsCount)
        {
            Logger.Warning("Cannot send missile {idx}: item {itemId} has invalid usage {usageIndex}/{usageCount}",
                payload.What.MissileIndex, missileItem.StringId, missionWeapon.CurrentUsageIndex, missionWeapon.WeaponsCount);
            return;
        }

        string itemModifierId = missionWeapon.ItemModifier?.StringId;
        if (missionWeapon.ItemModifier != null && string.IsNullOrEmpty(itemModifierId))
        {
            Logger.Warning("Missile {idx} item modifier has no StringId; sending the shot without it", payload.What.MissileIndex);
        }

        long shotSequence = System.Threading.Interlocked.Increment(ref nextShotSequence);
        RememberLocalShot(payload.What.MissileIndex, agentInfo.AgentId, shotSequence);

        Logger.Debug("Sending Agent Shoot sequence {sequence} with index {idx}, item {itemId}, modifier {modifierId}, usage {usageIndex}/{usageCount}",
            shotSequence, payload.What.MissileIndex, missileItem.StringId, itemModifierId,
            missionWeapon.CurrentUsageIndex, missionWeapon.WeaponsCount);

        NetworkAgentShoot message = new NetworkAgentShoot(
            agentInfo.AgentId,
            payload.What.Position,
            payload.What.Direction,
            payload.What.Orientation,
            payload.What.HasRigidBody,
            missileItem.StringId,
            itemModifierId,
            missionWeapon.Banner,
            payload.What.MissileIndex,
            payload.What.BaseSpeed,
            payload.What.Speed,
            missionWeapon.CurrentUsageIndex,
            shotSequence);

        network.SendAll(message);
    }

    private void AgentShootRecieve(MessagePayload<NetworkAgentShoot> payload)
    {
        NetworkAgentShoot shot = payload.What;
        long firstSeenTimestamp = Stopwatch.GetTimestamp();
        MarkReconstructionOutstanding(shot);

        GameThread.RunSafe(() =>
        {
            if (disposed)
            {
                CompleteReconstruction(shot);
                return;
            }

            try
            {
                ReconstructionResult result = TryReconstruct(shot, out string retryReason);
                if (result == ReconstructionResult.Retry)
                {
                    if (!EnqueuePending(shot, firstSeenTimestamp, retryReason))
                        CompleteReconstruction(shot);
                }
                else
                {
                    CompleteReconstruction(shot);
                }
            }
            catch
            {
                CompleteReconstruction(shot);
                throw;
            }
        });
    }

    public void DrainPendingShots()
    {
        if (disposed)
        {
            pendingShots.Clear();
            return;
        }

        int count = pendingShots.Count;
        for (int i = 0; i < count; i++)
        {
            PendingShot pending = pendingShots.Dequeue();
            double ageSeconds = ElapsedSeconds(pending.FirstSeenTimestamp);
            if (ageSeconds > PendingShotLifetimeSeconds)
            {
                Logger.Warning("Dropping pending missile {idx} for agent {agentId} after {ageMs:0}ms: {reason}",
                    pending.Shot.MissileIndex, pending.Shot.AgentId, ageSeconds * 1000d, pending.LastReason);
                CompleteReconstruction(pending.Shot);
                continue;
            }

            try
            {
                ReconstructionResult result = TryReconstruct(pending.Shot, out string retryReason);
                if (result == ReconstructionResult.Retry)
                    pendingShots.Enqueue(new PendingShot(pending.Shot, pending.FirstSeenTimestamp, retryReason));
                else
                    CompleteReconstruction(pending.Shot);
            }
            catch (Exception ex)
            {
                CompleteReconstruction(pending.Shot);
                Logger.Error(ex, "Failed to retry missile {MissileIndex} reconstruction", pending.Shot.MissileIndex);
            }
        }
    }

    public bool IsReconstructionPending(Guid agentId, long shotSequence, int sourceMissileIndex)
    {
        if (disposed)
            return false;

        lock (outstandingGate)
        {
            return outstandingReconstructions.ContainsKey(ReconstructionKey(agentId, shotSequence, sourceMissileIndex));
        }
    }

    public bool TryGetLocalShot(int sourceMissileIndex, out Guid agentId, out long shotSequence)
    {
        if (!disposed && localShots.TryGetValue(sourceMissileIndex, out LocalShot shot))
        {
            agentId = shot.AgentId;
            shotSequence = shot.ShotSequence;
            return true;
        }

        agentId = Guid.Empty;
        shotSequence = 0;
        return false;
    }

    public void RecordImpactHint(Guid attackerAgentId, long shotSequence, Guid victimAgentId,
        bool isMountFallback, Vec3 historicalImpactPosition, Vec3 impactVelocity)
    {
        if (disposed || attackerAgentId == Guid.Empty || shotSequence == 0)
            return;

        var key = (attackerAgentId, shotSequence);
        lock (outstandingGate)
        {
            if (disposed || completedShotSequences.Contains(key) || impactHints.ContainsKey(key))
                return;

            impactHints[key] = new ImpactHint(victimAgentId, isMountFallback, historicalImpactPosition,
                impactVelocity);
            impactHintHistory.Enqueue(key);

            while (impactHintHistory.Count > MaxImpactHints)
                impactHints.Remove(impactHintHistory.Dequeue());
        }
    }

    private void RememberLocalShot(int sourceMissileIndex, Guid agentId, long shotSequence)
    {
        localShots[sourceMissileIndex] = new LocalShot(agentId, shotSequence);
        localShotHistory.Enqueue((sourceMissileIndex, shotSequence));

        while (localShotHistory.Count > MaxLocalShotMappings)
        {
            var expired = localShotHistory.Dequeue();
            if (localShots.TryGetValue(expired.MissileIndex, out LocalShot current)
                && current.ShotSequence == expired.ShotSequence)
            {
                localShots.Remove(expired.MissileIndex);
            }
        }
    }

    private bool EnqueuePending(NetworkAgentShoot shot, long firstSeenTimestamp, string reason)
    {
        double ageSeconds = ElapsedSeconds(firstSeenTimestamp);
        if (ageSeconds > PendingShotLifetimeSeconds)
        {
            Logger.Warning("Dropping missile {idx} for agent {agentId} after {ageMs:0}ms: {reason}",
                shot.MissileIndex, shot.AgentId, ageSeconds * 1000d, reason);
            return false;
        }

        if (pendingShots.Count >= MaxPendingShots)
        {
            PendingShot oldest = pendingShots.Dequeue();
            Logger.Warning("Dropping pending missile {idx} for agent {agentId}: reconstruction queue reached {capacity}",
                oldest.Shot.MissileIndex, oldest.Shot.AgentId, MaxPendingShots);
            CompleteReconstruction(oldest.Shot);
        }

        pendingShots.Enqueue(new PendingShot(shot, firstSeenTimestamp, reason));
        return true;
    }

    private void MarkReconstructionOutstanding(NetworkAgentShoot shot)
    {
        lock (outstandingGate)
        {
            var key = ReconstructionKey(shot.AgentId, shot.ShotSequence, shot.MissileIndex);
            outstandingReconstructions.TryGetValue(key, out int count);
            outstandingReconstructions[key] = count + 1;
        }
    }

    private void CompleteReconstruction(NetworkAgentShoot shot)
    {
        lock (outstandingGate)
        {
            var key = ReconstructionKey(shot.AgentId, shot.ShotSequence, shot.MissileIndex);
            var sequenceKey = (shot.AgentId, shot.ShotSequence);
            if (shot.ShotSequence != 0)
                impactHints.Remove(sequenceKey);

            if (!outstandingReconstructions.TryGetValue(key, out int count))
                return;

            if (count <= 1)
            {
                outstandingReconstructions.Remove(key);
                RememberCompletedShot(sequenceKey);
            }
            else
                outstandingReconstructions[key] = count - 1;
        }
    }

    private bool TryTakeImpactHint(NetworkAgentShoot shot, out ImpactHint hint)
    {
        hint = default;
        if (shot.ShotSequence == 0)
            return false;

        lock (outstandingGate)
        {
            var key = (shot.AgentId, shot.ShotSequence);
            if (!impactHints.TryGetValue(key, out hint))
                return false;

            impactHints.Remove(key);
            return true;
        }
    }

    private void RememberCompletedShot((Guid AgentId, long ShotSequence) key)
    {
        if (key.ShotSequence == 0 || !completedShotSequences.Add(key))
            return;

        completedShotHistory.Enqueue(key);
        while (completedShotHistory.Count > MaxCompletedShotSequences)
            completedShotSequences.Remove(completedShotHistory.Dequeue());
    }

    private static (Guid AgentId, long ShotSequence, int MissileIndex) ReconstructionKey(
        Guid agentId, long shotSequence, int sourceMissileIndex) =>
        (agentId, shotSequence, shotSequence == 0 ? sourceMissileIndex : 0);

    private static double ElapsedSeconds(long firstSeenTimestamp) =>
        (Stopwatch.GetTimestamp() - firstSeenTimestamp) / (double)Stopwatch.Frequency;

    private static long CreateShotSequenceSeed() =>
        BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & 0x3FFFFFFFFFFFFFFF;

    private ReconstructionResult TryReconstruct(NetworkAgentShoot shot, out string retryReason)
    {
        retryReason = null;

        Mission mission = Mission.Current;
        if (mission == null)
        {
            retryReason = "the mission is not ready";
            return ReconstructionResult.Retry;
        }

        Agent agent = null;
        if (networkAgentRegistry.TryGetAgentInfo(shot.AgentId, out var agentInfo)
            && agentInfo.Agent != null
            && agentInfo.Agent.Mission == mission
            && agentInfo.Agent.IsActive()
            && !networkAgentRegistry.IsLocallyControlled(agentInfo.Agent))
        {
            agent = agentInfo.Agent;
        }
        else if (BattleSpawnGate.IsCoopBattleActive)
        {
            agent = FindStandInShooter(mission);
        }

        if (agent == null)
        {
            retryReason = "no safe cosmetic shooter is active in the current mission";
            return ReconstructionResult.Retry;
        }

        if (string.IsNullOrEmpty(shot.MissileItemId))
        {
            Logger.Warning("Cannot reconstruct missile {idx}: its item id is empty", shot.MissileIndex);
            return ReconstructionResult.Dropped;
        }

        ItemObject missileItem = MBObjectManager.Instance.GetObject<ItemObject>(shot.MissileItemId);
        if (missileItem == null)
        {
            Logger.Warning("Cannot reconstruct missile {idx}: item {itemId} was not found", shot.MissileIndex, shot.MissileItemId);
            return ReconstructionResult.Dropped;
        }

        ItemModifier itemModifier = null;
        if (!string.IsNullOrEmpty(shot.ItemModifierId))
        {
            itemModifier = MBObjectManager.Instance.GetObject<ItemModifier>(shot.ItemModifierId);
            if (itemModifier == null)
            {
                Logger.Warning("Missile {idx} item modifier {modifierId} was not found; reconstructing without it",
                    shot.MissileIndex, shot.ItemModifierId);
            }
        }

        MissionWeapon missileWeapon = new MissionWeapon(missileItem, itemModifier, shot.Banner);
        if (missileWeapon.IsEmpty || shot.CurrentUsageIndex < 0 ||
            shot.CurrentUsageIndex >= missileWeapon.WeaponsCount)
        {
            Logger.Warning("Cannot reconstruct missile {idx}: item {itemId} has invalid usage {usageIndex}/{usageCount}",
                shot.MissileIndex, shot.MissileItemId, shot.CurrentUsageIndex, missileWeapon.WeaponsCount);
            return ReconstructionResult.Dropped;
        }

        missileWeapon.CurrentUsageIndex = shot.CurrentUsageIndex;
        missileWeapon.Amount = 1;

        Logger.Debug("Reconstructing missile sequence {sequence} with source id {id}, item {itemId}, modifier {modifierId}, usage {usageIndex}/{usageCount}",
            shot.ShotSequence, shot.MissileIndex, shot.MissileItemId, shot.ItemModifierId,
            shot.CurrentUsageIndex, missileWeapon.WeaponsCount);

        bool hasImpactHint = TryTakeImpactHint(shot, out ImpactHint impactHint);
        Vec3 impactTarget = default;
        bool usedCurrentTarget = false;
        bool hasImpactTarget = hasImpactHint
            && TryResolveImpactTarget(impactHint, mission, out impactTarget, out usedCurrentTarget);
        MissileReplayPlan replay = MissileReplayPlanner.Plan(shot.Position, shot.Velocity, shot.Orientation,
            shot.Speed, hasImpactTarget, impactTarget, impactHint.ImpactVelocity);

        if (replay.IsFastForwarded)
        {
            Logger.Debug("Fast-forwarding missile sequence {sequence} to a {durationMs:0}ms final segment aimed at the {targetKind} victim position",
                shot.ShotSequence, replay.RemainingFlightSeconds * 1000f,
                usedCurrentTarget ? "current" : "historical fallback");
        }

        Vec3 position = replay.Position;
        Vec3 direction = replay.Direction;
        Mat3 orientation = replay.Orientation;
        float baseSpeed = !float.IsNaN(shot.BaseSpeed) && !float.IsInfinity(shot.BaseSpeed) && shot.BaseSpeed > 0f
            ? shot.BaseSpeed
            : replay.Speed;

        WeaponData weaponData = missileWeapon.GetWeaponData(true);
        int index;
        GameEntity missileEntity;
        try
        {
            if (missileWeapon.WeaponsCount == 1)
            {
                WeaponStatsData weaponStatsData = missileWeapon.GetWeaponStatsDataForUsage(0);
                index = mission.AddMissileSingleUsageAux(-1, false, agent, in weaponData, in weaponStatsData, 0f,
                    ref position, ref direction, ref orientation, baseSpeed, replay.Speed, shot.HasRigidBody,
                    WeakGameEntity.Invalid, false, out missileEntity);
            }
            else
            {
                WeaponStatsData[] weaponStatsData = missileWeapon.GetWeaponStatsData();
                index = mission.AddMissileAux(-1, false, agent, in weaponData, weaponStatsData, 0f,
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
            Logger.Warning("Cannot reconstruct missile {idx}: native add returned no entity", shot.MissileIndex);
            return ReconstructionResult.Dropped;
        }

        // Track the missile in BOTH collections like vanilla OnAgentShootMissile: the engine hard-indexes
        // _missilesDictionary by the missile index on every collision, so a list-only add crashes on the first hit.
        // Indexer, not Add, so a reused engine index overwrites rather than throwing back out of the dictionary.
        Mission.Missile missileRecord = new Mission.Missile(mission, index, missileEntity, agent, missileWeapon, null);
        mission._missilesList.Add(missileRecord);
        mission._missilesDictionary[index] = missileRecord;

        Logger.Debug("Reconstructed missile sequence {sequence}, source {sourceIndex}, as local {localIndex}",
            shot.ShotSequence, shot.MissileIndex, index);
        messageBroker.Publish(this, new MissileReconstructed(shot.AgentId, shot.MissileIndex, shot.ShotSequence,
            position, direction, baseSpeed, replay.Speed, replay.IsFastForwarded,
            replay.RemainingFlightSeconds));
        return ReconstructionResult.Reconstructed;
    }

    private bool TryResolveImpactTarget(ImpactHint hint, Mission mission, out Vec3 impactTarget,
        out bool usedCurrentTarget)
    {
        usedCurrentTarget = false;
        if (networkAgentRegistry.TryGetAgentInfo(hint.VictimAgentId, out var victimInfo))
        {
            Agent victim = hint.IsMountFallback ? victimInfo.Agent?.MountAgent : victimInfo.Agent;
            if (victim != null && victim.Mission == mission && victim.IsActive())
            {
                Vec3 currentChest = victim.GetChestGlobalPosition();
                if (MissileReplayPlanner.IsFinite(currentChest))
                {
                    impactTarget = currentChest;
                    usedCurrentTarget = true;
                    return true;
                }
            }
        }

        impactTarget = hint.HistoricalImpactPosition;
        return MissileReplayPlanner.IsFinite(impactTarget);
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
}
