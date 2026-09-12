using Common.Messaging;
using Missions.Agents.Messages;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

public interface IGuardedHitWindow : IDisposable
{
    long Epoch { get; }
    void Advance();
    long RegisterCandidate(
        Agent affectedAgent,
        Agent affectorAgent,
        in Blow blow,
        in AttackCollisionData collisionData);
    bool CompleteCandidate(long candidateId);
    void Reset();
}

public sealed class GuardedHitWindow : IGuardedHitWindow
{
    private readonly IMessageBroker messageBroker;
    private readonly Dictionary<long, GuardCandidate> candidates = new();
    private readonly Dictionary<HitPair, List<long>> candidatesByPair = new();
    private long nextCandidateId;
    private bool disposed;

    public long Epoch { get; private set; }

    public GuardedHitWindow(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<LocalAgentGuardedHit>(
            Handle_LocalAgentGuardedHit);
    }

    public void Advance()
    {
        if (!disposed)
            Epoch++;
    }

    public long RegisterCandidate(
        Agent affectedAgent,
        Agent affectorAgent,
        in Blow blow,
        in AttackCollisionData collisionData)
    {
        if (disposed ||
            affectedAgent == null ||
            affectorAgent == null)
        {
            return 0;
        }

        long candidateId = ++nextCandidateId;
        var pair = new HitPair(affectedAgent, affectorAgent);
        candidates.Add(
            candidateId,
            new GuardCandidate(
                pair,
                blow,
                collisionData,
                Epoch));

        if (!candidatesByPair.TryGetValue(
                pair,
                out List<long> candidateIds))
        {
            candidateIds = new List<long>();
            candidatesByPair.Add(pair, candidateIds);
        }

        candidateIds.Add(candidateId);
        return candidateId;
    }

    public bool CompleteCandidate(long candidateId)
    {
        if (disposed ||
            candidateId == 0 ||
            !candidates.TryGetValue(
                candidateId,
                out GuardCandidate candidate))
        {
            return false;
        }

        candidates.Remove(candidateId);
        if (candidatesByPair.TryGetValue(
                candidate.Pair,
                out List<long> candidateIds))
        {
            candidateIds.Remove(candidateId);
            if (candidateIds.Count == 0)
                candidatesByPair.Remove(candidate.Pair);
        }

        return candidate.IsGuarded;
    }

    public void Reset()
    {
        candidates.Clear();
        candidatesByPair.Clear();
        nextCandidateId = 0;
        Epoch = 0;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        messageBroker.Unsubscribe<LocalAgentGuardedHit>(
            Handle_LocalAgentGuardedHit);
        candidates.Clear();
        candidatesByPair.Clear();
    }

    private void Handle_LocalAgentGuardedHit(
        MessagePayload<LocalAgentGuardedHit> payload)
    {
        LocalAgentGuardedHit guardedHit = payload.What;
        if (disposed ||
            guardedHit?.AffectedAgent == null ||
            guardedHit.AffectorAgent == null)
        {
            return;
        }

        var pair = new HitPair(
            guardedHit.AffectedAgent,
            guardedHit.AffectorAgent);
        if (!candidatesByPair.TryGetValue(
                pair,
                out List<long> candidateIds))
        {
            // Clean blocks can produce this callback without RegisterBlow, so unmatched evidence must be discarded.
            return;
        }

        for (int i = candidateIds.Count - 1; i >= 0; i--)
        {
            if (!candidates.TryGetValue(
                    candidateIds[i],
                    out GuardCandidate candidate))
            {
                candidateIds.RemoveAt(i);
                continue;
            }

            if (candidate.Epoch != Epoch ||
                !IsSameCollision(
                    candidate,
                    guardedHit))
            {
                continue;
            }

            candidate.IsGuarded = true;
            return;
        }
    }

    private static bool IsSameCollision(
        GuardCandidate candidate,
        LocalAgentGuardedHit guardedHit)
    {
        Blow candidateBlow = candidate.Blow;
        Blow guardedBlow = guardedHit.Blow;
        AttackCollisionData candidateCollision =
            candidate.CollisionData;
        AttackCollisionData guardedCollision =
            guardedHit.CollisionData;

        // Damage and block-result fields differ between callbacks, so compare only stable collision data.
        return candidateBlow.IsMissile == guardedBlow.IsMissile
            && candidateBlow.AttackType == guardedBlow.AttackType
            && candidateBlow.StrikeType == guardedBlow.StrikeType
            && candidateBlow.DamageType == guardedBlow.DamageType
            && candidateBlow.BoneIndex == guardedBlow.BoneIndex
            && SameVector(
                candidateBlow.GlobalPosition,
                guardedBlow.GlobalPosition)
            && SameVector(
                candidateBlow.Direction,
                guardedBlow.Direction)
            && SameVector(
                candidateBlow.SwingDirection,
                guardedBlow.SwingDirection)
            && candidateCollision
                   .AffectorWeaponSlotOrMissileIndex
               == guardedCollision
                   .AffectorWeaponSlotOrMissileIndex
            && candidateCollision.StrikeType
               == guardedCollision.StrikeType
            && candidateCollision.DamageType
               == guardedCollision.DamageType
            && candidateCollision.CollisionBoneIndex
               == guardedCollision.CollisionBoneIndex
            && candidateCollision.AttackBoneIndex
               == guardedCollision.AttackBoneIndex
            && candidateCollision.AttackDirection
               == guardedCollision.AttackDirection
            && candidateCollision.AttackProgress
               == guardedCollision.AttackProgress
            && candidateCollision.CollisionDistanceOnWeapon
               == guardedCollision.CollisionDistanceOnWeapon
            && SameVector(
                candidateCollision.CollisionGlobalPosition,
                guardedCollision.CollisionGlobalPosition);
    }

    private static bool SameVector(Vec3 left, Vec3 right)
    {
        return left.x == right.x
            && left.y == right.y
            && left.z == right.z;
    }

    private sealed class GuardCandidate
    {
        public HitPair Pair { get; }
        public Blow Blow { get; }
        public AttackCollisionData CollisionData { get; }
        public long Epoch { get; }
        public bool IsGuarded { get; set; }

        public GuardCandidate(
            HitPair pair,
            in Blow blow,
            in AttackCollisionData collisionData,
            long epoch)
        {
            Pair = pair;
            Blow = blow;
            CollisionData = collisionData;
            Epoch = epoch;
        }
    }

    private readonly struct HitPair : IEquatable<HitPair>
    {
        private readonly Agent affectedAgent;
        private readonly Agent affectorAgent;

        public HitPair(
            Agent affectedAgent,
            Agent affectorAgent)
        {
            this.affectedAgent = affectedAgent;
            this.affectorAgent = affectorAgent;
        }

        public bool Equals(HitPair other)
        {
            return ReferenceEquals(
                    affectedAgent,
                    other.affectedAgent)
                && ReferenceEquals(
                    affectorAgent,
                    other.affectorAgent);
        }

        public override bool Equals(object obj)
        {
            return obj is HitPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (RuntimeHelpers.GetHashCode(affectedAgent) * 397)
                ^ RuntimeHelpers.GetHashCode(affectorAgent);
        }
    }
}
