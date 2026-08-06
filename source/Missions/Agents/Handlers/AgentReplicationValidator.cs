using GameInterface.Services.MapEvents;
using Missions.Agents.Packets;
using Missions.Data;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IAgentReplicationValidator
{
    bool TryValidate(MovementPacket packet, out string failure);
    bool TryValidate(MountMovementPacket packet, out string failure);
    bool TryValidate(AgentActionPacket packet, out string failure);
    bool TryValidate(NetworkMissionJoinInfo joinInfo, out string failure);
    bool ShouldLogRejection(out int suppressedRejections);
    bool ShouldLogApplicationFailure(out int suppressedFailures);
}

public sealed class AgentReplicationValidator : IAgentReplicationValidator
{
    internal const int MaximumControllerIdLength = 128;
    internal const int MaximumObjectIdLength = 256;
    internal const float MaximumCoordinateMagnitude = 1000000f;
    internal const float MaximumMovementSpeed = 100f;
    internal const float MaximumHealth = 10000f;
    internal const int MaximumBattleHostEpoch = 1000000;
    internal const long MaximumActionSequence = 1000000000L;
    private const int LinearDuplicateCheckLimit = 16;

    private static readonly long RejectionLogIntervalTicks =
        Stopwatch.Frequency;
    private static readonly ulong AllowedAnimationFlags =
        (ulong)AnimFlags.anf_animation_layer_flags_mask |
        ((ulong)AnimFlags.anf_animation_layer_flags_mask - 1UL) |
        (ulong)AnimFlags.anf_randomization_weight_mask;
    private static readonly uint AllowedEventFlags = 0x7FFFFu;
    private static readonly uint AllowedLocomotionFlags =
        (uint)Agent.MovementControlFlag.MoveMask;
    private static readonly uint AllowedActionMovementFlags =
        (uint)AgentActionData.DefendMovementFlagsMask;

    private readonly object validationGate = new object();
    private readonly int maximumMissionAgents;
    private readonly int maximumActionCount;
    private readonly HashSet<ushort> compactIds = new HashSet<ushort>();
    private readonly HashSet<Guid> canonicalIds = new HashSet<Guid>();
    private long nextRejectionLog;
    private int suppressedRejections;
    private long nextApplicationFailureLog;
    private int suppressedApplicationFailures;

    public AgentReplicationValidator(
        IBattleAgentBudget agentBudget,
        IAnimationActionCountProvider actionCountProvider)
        : this(
            agentBudget?.MaxRenderedAgents ?? 2000,
            actionCountProvider?.GetActionCount() ?? 0)
    {
    }

    internal AgentReplicationValidator(
        int maximumMissionAgents,
        int maximumActionCount)
    {
        if (maximumMissionAgents <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMissionAgents));
        if (maximumActionCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumActionCount));
        this.maximumMissionAgents = maximumMissionAgents;
        this.maximumActionCount = maximumActionCount;
    }

    public bool TryValidate(MovementPacket packet, out string failure)
    {
#if DEBUG
        lock (validationGate)
#endif
        {
            if (!TryValidateIds(
                    packet.IdentityScopeId,
                    packet.AgentIds,
                    packet.AgentGuids,
                    packet.Agents?.Length ?? 0,
                    out failure))
            {
                return false;
            }

            if (packet.Agents == null)
            {
                failure = "movement data is missing";
                return false;
            }

            for (int i = 0; i < packet.Agents.Length; i++)
            {
                AgentData data = packet.Agents[i];
                if (!TryValidateAgentData(data, out failure))
                {
                    failure = $"movement entry {i}: {failure}";
                    return false;
                }
            }

            failure = null;
            return true;
        }
    }

    public bool TryValidate(MountMovementPacket packet, out string failure)
    {
#if DEBUG
        lock (validationGate)
#endif
        {
            if (!TryValidateIds(
                    packet.IdentityScopeId,
                    packet.MountIds,
                    packet.MountGuids,
                    packet.Mounts?.Length ?? 0,
                    out failure))
            {
                return false;
            }

            if (packet.Mounts == null)
            {
                failure = "mount movement data is missing";
                return false;
            }

            for (int i = 0; i < packet.Mounts.Length; i++)
            {
                if (!TryValidateMountData(packet.Mounts[i], out failure))
                {
                    failure = $"mount movement entry {i}: {failure}";
                    return false;
                }
            }

            failure = null;
            return true;
        }
    }

    public bool TryValidate(AgentActionPacket packet, out string failure)
    {
#if DEBUG
        lock (validationGate)
#endif
        {
            int count = packet.AgentIds?.Length ?? 0;
            if (!IsValidRequiredId(packet.ControllerId, MaximumControllerIdLength))
            {
                failure = "action controller id is missing or too long";
                return false;
            }
            if (count == 0 || count > maximumMissionAgents ||
                packet.Actions == null || packet.Sequences == null ||
                packet.Actions.Length != count || packet.Sequences.Length != count)
            {
                failure = "action arrays are missing, empty, excessive, or mismatched";
                return false;
            }
            if (packet.BattleHostEpoch < 0 ||
                packet.BattleHostEpoch > MaximumBattleHostEpoch)
            {
                failure = "action host epoch is negative or unreasonable";
                return false;
            }

            canonicalIds.Clear();
            for (int i = 0; i < count; i++)
            {
                Guid agentId = packet.AgentIds[i];
                if (agentId == Guid.Empty || !canonicalIds.Add(agentId))
                {
                    failure = $"action agent id {i} is empty or duplicated";
                    return false;
                }
                if (packet.Sequences[i] <= 0 ||
                    packet.Sequences[i] > MaximumActionSequence)
                {
                    failure = $"action sequence {i} is not positive or reasonable";
                    return false;
                }
                if (!TryValidateActionData(packet.Actions[i], out failure))
                {
                    failure = $"action entry {i}: {failure}";
                    return false;
                }
            }

            failure = null;
            return true;
        }
    }

    public bool TryValidate(NetworkMissionJoinInfo joinInfo, out string failure)
    {
#if DEBUG
        lock (validationGate)
#endif
        {
            if (joinInfo == null)
            {
                failure = "join info is missing";
                return false;
            }
            if (!IsValidRequiredId(joinInfo.ControllerId, MaximumControllerIdLength))
            {
                failure = "join controller id is missing or too long";
                return false;
            }

            CoopAgentSpawnData[] agents = joinInfo.AiAgentData;
            if (agents == null || agents.Length == 0 || agents.Length > maximumMissionAgents)
            {
                failure = "join agent array is missing, empty, or excessive";
                return false;
            }

            canonicalIds.Clear();
            for (int i = 0; i < agents.Length; i++)
            {
                CoopAgentSpawnData agent = agents[i];
                if (agent == null || agent.AgentId == Guid.Empty ||
                    !canonicalIds.Add(agent.AgentId))
                {
                    failure = $"join agent {i} is missing, empty, or duplicated";
                    return false;
                }
                if (!IsValidRequiredId(agent.CharacterObjectId, MaximumObjectIdLength))
                {
                    failure = $"join character id {i} is missing or too long";
                    return false;
                }
                if (!IsReasonablePosition(agent.Position) ||
                    !IsFinite(agent.Health) || agent.Health < 0f || agent.Health > MaximumHealth)
                {
                    failure = $"join spawn state {i} is nonfinite or unreasonable";
                    return false;
                }
            }

            failure = null;
            return true;
        }
    }

    public bool ShouldLogRejection(out int suppressed)
    {
        lock (validationGate)
        {
            long now = Stopwatch.GetTimestamp();
            if (now < nextRejectionLog)
            {
                suppressedRejections++;
                suppressed = 0;
                return false;
            }

            nextRejectionLog = now + RejectionLogIntervalTicks;
            suppressed = suppressedRejections;
            suppressedRejections = 0;
            return true;
        }
    }

    public bool ShouldLogApplicationFailure(out int suppressed)
    {
        lock (validationGate)
        {
            long now = Stopwatch.GetTimestamp();
            if (now < nextApplicationFailureLog)
            {
                suppressedApplicationFailures++;
                suppressed = 0;
                return false;
            }

            nextApplicationFailureLog = now + RejectionLogIntervalTicks;
            suppressed = suppressedApplicationFailures;
            suppressedApplicationFailures = 0;
            return true;
        }
    }

    private bool TryValidateIds(
        string identityScopeId,
        ushort[] ids,
        Guid[] guids,
        int dataCount,
        out string failure)
    {
        bool compact = ids != null;
        bool canonical = guids != null;
        if (compact == canonical)
        {
            failure = "exactly one id representation is required";
            return false;
        }

        int idCount = compact ? ids.Length : guids.Length;
        if (idCount == 0 || idCount > maximumMissionAgents || dataCount != idCount)
        {
            failure = "id and data arrays are empty, excessive, or mismatched";
            return false;
        }

        if (compact)
        {
            if (!IsValidRequiredId(identityScopeId, MaximumControllerIdLength))
            {
                failure = "compact ids require a bounded identity scope";
                return false;
            }

            if (ids.Length <= LinearDuplicateCheckLimit)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] == 0 || HasEarlier(ids, i))
                    {
                        failure = $"compact id {i} is zero or duplicated";
                        return false;
                    }
                }
            }
            else
            {
                compactIds.Clear();
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] == 0 || !compactIds.Add(ids[i]))
                    {
                        failure = $"compact id {i} is zero or duplicated";
                        return false;
                    }
                }
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(identityScopeId))
            {
                failure = "canonical ids cannot include an identity scope";
                return false;
            }

            if (guids.Length <= LinearDuplicateCheckLimit)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    if (guids[i] == Guid.Empty || HasEarlier(guids, i))
                    {
                        failure = $"canonical id {i} is empty or duplicated";
                        return false;
                    }
                }
            }
            else
            {
                canonicalIds.Clear();
                for (int i = 0; i < guids.Length; i++)
                {
                    if (guids[i] == Guid.Empty || !canonicalIds.Add(guids[i]))
                    {
                        failure = $"canonical id {i} is empty or duplicated";
                        return false;
                    }
                }
            }
        }

        failure = null;
        return true;
    }

    private bool TryValidateAgentData(AgentData data, out string failure)
    {
        if (!IsReasonablePosition(data.Position) ||
            !IsReasonableDirection(data.LookDirection, allowZero: false) ||
            !IsReasonableDirection(data.MovementDirection, allowZero: true) ||
            !IsReasonableInput(data.InputVector) ||
            !IsFinite(data.Speed) || data.Speed < 0f || data.Speed > MaximumMovementSpeed)
        {
            failure = "rider position, direction, input, or speed is nonfinite or unreasonable";
            return false;
        }
        if ((data.MovementFlag & ~AllowedLocomotionFlags) != 0)
        {
            failure = "rider locomotion flags contain unsupported bits";
            return false;
        }
        if (data.MountData != null && !TryValidateMountData(data.MountData, out failure))
            return false;

        failure = null;
        return true;
    }

    private bool TryValidateMountData(AgentMountData data, out string failure)
    {
        if (data == null)
        {
            failure = "mount data is missing";
            return false;
        }
        if (!IsReasonablePosition(data.MountPosition) ||
            !IsReasonableDirection(data.MountLookDirection, allowZero: false) ||
            !IsReasonableDirection(data.MountMovementDirection, allowZero: true) ||
            !IsReasonableInput(data.MountInputVector) ||
            !IsFinite(data.MountSpeed) || data.MountSpeed < 0f ||
            data.MountSpeed > MaximumMovementSpeed)
        {
            failure = "mount position, direction, input, or speed is nonfinite or unreasonable";
            return false;
        }
        if ((data.MountMovementFlag & ~AllowedLocomotionFlags) != 0)
        {
            failure = "mount locomotion flags contain unsupported bits";
            return false;
        }
        if (!IsProgress(data.MountAction0Progress) ||
            !IsProgress(data.MountAction1Progress) ||
            !IsFinite(data.MountAction0Speed) || data.MountAction0Speed < 0f ||
            data.MountAction0Speed > 8f)
        {
            failure = "mount action progress or speed is nonfinite or unreasonable";
            return false;
        }
        if (!IsActionIndex(data.MountAction0Index) ||
            !IsActionIndex(data.MountAction1Index) ||
            !IsActionIndex(data.MountAction0TurnActionIndex))
        {
            failure = "mount action index is outside the bounded wire range";
            return false;
        }
        if ((data.MountAction0Flag & ~AllowedAnimationFlags) != 0 ||
            (data.MountAction1Flag & ~AllowedAnimationFlags) != 0)
        {
            failure = "mount animation flags contain unsupported bits";
            return false;
        }
        if (data.MountAction0TurnDirection < AgentMountData.TurnLeft ||
            data.MountAction0TurnDirection > AgentMountData.TurnRight)
        {
            failure = "mount turn direction is invalid";
            return false;
        }
        if (!string.IsNullOrEmpty(data.MountIdentityScopeId) &&
            data.MountIdentityScopeId.Length > MaximumControllerIdLength)
        {
            failure = "mount identity scope is too long";
            return false;
        }
        if (data.MountMovementId != 0 && data.MountAgentId != Guid.Empty)
        {
            failure = "mount compact and canonical ids cannot both be set";
            return false;
        }
        if (data.MountMovementId == 0 &&
            !string.IsNullOrEmpty(data.MountIdentityScopeId))
        {
            failure = "mount identity scope requires a compact id";
            return false;
        }

        failure = null;
        return true;
    }

    private bool TryValidateActionData(AgentActionData data, out string failure)
    {
        if (data == null)
        {
            failure = "action data is missing";
            return false;
        }
        if (!IsProgress(data.Action0Progress) || !IsProgress(data.Action1Progress))
        {
            failure = "action progress is nonfinite or outside 0..1";
            return false;
        }
        if (!IsActionIndex(data.Action0Index) || !IsActionIndex(data.Action1Index))
        {
            failure = "action index is outside the bounded wire range";
            return false;
        }
        if ((data.Action0Flag & ~AllowedAnimationFlags) != 0 ||
            (data.Action1Flag & ~AllowedAnimationFlags) != 0)
        {
            failure = "action animation flags contain unsupported bits";
            return false;
        }
        if ((data.MovementFlag & ~AllowedActionMovementFlags) != 0 ||
            (data.EventFlag & ~AllowedEventFlags) != 0)
        {
            failure = "action movement or event flags contain unsupported bits";
            return false;
        }
        if (data.GuardState < 0 || data.GuardState > 4 ||
            !IsChannel(data.GuardPresentationChannel) ||
            !IsChannel(data.GuardActionChannel))
        {
            failure = "action guard state or channel is invalid";
            return false;
        }
        if ((data.GuardActionIsDefending || data.GuardActionIsReaction) &&
            data.GuardActionChannel < 0)
        {
            failure = "action guard metadata has no action channel";
            return false;
        }

        failure = null;
        return true;
    }

    private static bool IsValidRequiredId(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    private static bool HasEarlier(ushort[] values, int index)
    {
        ushort value = values[index];
        for (int i = 0; i < index; i++)
        {
            if (values[i] == value) return true;
        }
        return false;
    }

    private static bool HasEarlier(Guid[] values, int index)
    {
        Guid value = values[index];
        for (int i = 0; i < index; i++)
        {
            if (values[i] == value) return true;
        }
        return false;
    }

    private static bool IsReasonablePosition(Vec3 value) =>
        IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z) &&
        Math.Abs(value.X) <= MaximumCoordinateMagnitude &&
        Math.Abs(value.Y) <= MaximumCoordinateMagnitude &&
        Math.Abs(value.Z) <= MaximumCoordinateMagnitude;

    private static bool IsReasonableDirection(Vec2 value, bool allowZero)
    {
        if (!IsFinite(value.X) || !IsFinite(value.Y)) return false;
        float lengthSquared = value.LengthSquared;
        return lengthSquared <= 1.21f && (allowZero || lengthSquared >= 0.01f);
    }

    private static bool IsReasonableDirection(Vec3 value, bool allowZero)
    {
        if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z)) return false;
        float lengthSquared = value.LengthSquared;
        return lengthSquared <= 1.21f && (allowZero || lengthSquared >= 0.01f);
    }

    private static bool IsReasonableInput(Vec2 value) =>
        IsFinite(value.X) && IsFinite(value.Y) && value.LengthSquared <= 2.25f;

    private static bool IsProgress(float value) =>
        IsFinite(value) && value >= 0f && value <= 1f;

    private bool IsActionIndex(int value) =>
        value >= AgentMountData.NoActionIndex && value < maximumActionCount;

    private static bool IsChannel(int value) => value >= -1 && value <= 1;

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
