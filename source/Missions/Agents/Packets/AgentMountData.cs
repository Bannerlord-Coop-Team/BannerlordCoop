using ProtoBuf;
using System;
using System.Collections.Concurrent;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentMountData
    {
        internal const float StationarySpeedThreshold = 0.05f;
        internal const int TurnLeft = -1;
        internal const int NoTurn = 0;
        internal const int TurnRight = 1;
        internal const int NoActionIndex = -1;
        private static readonly ConcurrentDictionary<int, bool> locomotionActionCache =
            new ConcurrentDictionary<int, bool>();
        private static readonly ConcurrentDictionary<int, TurnActionClassification> turnActionCache =
            new ConcurrentDictionary<int, TurnActionClassification>();

        // The parameter is the MOUNT agent itself (callers pass rider.MountAgent), so read it directly —
        // mirroring ApplyMount. Dereferencing .MountAgent here was reading the mount's own (null) mount → NRE.
        public AgentMountData(
            Agent mountAgent,
            ushort mountMovementId = 0,
            string mountIdentityScopeId = null,
            Guid mountAgentId = default,
            float? mountAction0Speed = null,
            bool? mountAction0IsLocomotion = null,
            int? mountAction0TurnDirection = null,
            int? mountAction0TurnActionIndex = null,
            float? mountAction0TurnProgress = null,
            bool? mountAction0IsSyntheticTurn = null)
        {
            MountInputVector = mountAgent.MovementInputVector;
            MountAction0Index = mountAgent.GetCurrentAction(0).Index;
            bool syntheticStationaryTurn =
                mountAction0IsSyntheticTurn
                ?? (mountAction0TurnDirection.HasValue
                    && mountAction0TurnDirection.Value != NoTurn
                    && mountAction0TurnActionIndex.HasValue
                    && MountAction0Index != mountAction0TurnActionIndex.Value);
            MountAction0IsSyntheticTurn = syntheticStationaryTurn;
            MountAction0Flag = syntheticStationaryTurn
                ? 0UL
                : (ulong)mountAgent.GetCurrentAnimationFlag(0);
            MountAction0Progress = syntheticStationaryTurn
                ? mountAction0TurnProgress ?? 0f
                : mountAgent.GetCurrentActionProgress(0);
            GetRenderedAction0State(
                mountAgent,
                out string renderedAction0Animation,
                out float renderedAction0Speed);
            MountAction1Flag = (ulong)mountAgent.GetCurrentAnimationFlag(1);
            MountAction1Progress = mountAgent.GetCurrentActionProgress(1);
            MountAction1Index = mountAgent.GetCurrentAction(1).Index;
            MountLookDirection = mountAgent.LookDirection;
            MountMovementDirection = mountAgent.GetMovementDirection();
            MountPosition = mountAgent.Position;
            MountSpeed = mountAgent.GetRealGlobalVelocity().AsVec2.Length;
            MountMovementFlag = (uint)AgentData.GetLocomotionMovementFlags(
                mountAgent.MovementFlags);
            MountMovementId = mountMovementId;
            MountIdentityScopeId = mountIdentityScopeId;
            MountAgentId = mountAgentId;
            MountAction0Speed = syntheticStationaryTurn
                ? 1f
                : mountAction0Speed ?? renderedAction0Speed;
            MountAction0IsLocomotion = !syntheticStationaryTurn
                && (mountAction0IsLocomotion
                    ?? IsLocomotionAction(MountAction0Index, renderedAction0Animation));
            MountAction0TurnDirection = mountAction0TurnDirection
                ?? GetTurnDirection(MountAction0Index, renderedAction0Animation);
            MountAction0TurnActionIndex = mountAction0TurnActionIndex
                ?? ResolveStationaryTurnActionIndex(
                    MountAction0Index,
                    MountAction0TurnDirection,
                    mountAgent.Monster?.MonsterUsage);
        }

        public AgentMountData(Agent mountAgent, Guid mountAgentId)
            : this(mountAgent, 0, null, mountAgentId)
        {
        }

        public void ApplyMount(Agent mountAgent)
        {
            // NOTE: mount position is NOT applied here — it is reconciled per-frame by AgentPositionInterpolator
            // (fed MountPosition by AgentMovementHandler). Everything below is per-packet mount state/animation.
            mountAgent.SetMovementDirection(MountMovementDirection);

            // A Controller.None puppet cannot select its channel-zero stand, turn, or gait action.
            bool stationaryTurn = MountSpeed <= StationarySpeedThreshold
                && MountAction0TurnDirection != NoTurn;
            int desiredAction0Index = ResolveAction0Index(
                MountAction0Index,
                MountSpeed,
                MountAction0IsLocomotion,
                MountAction0TurnDirection,
                MountAction0TurnActionIndex);
            bool syntheticStationaryTurn = stationaryTurn
                && MountAction0IsSyntheticTurn;
            ActionIndexCache currentAction0 = mountAgent.GetCurrentAction(0);
            bool nativeTurnFlagsChanged = stationaryTurn
                && !syntheticStationaryTurn
                && currentAction0.Index == desiredAction0Index
                && (ulong)mountAgent.GetCurrentAnimationFlag(0) != MountAction0Flag;
            if (!syntheticStationaryTurn)
            {
                if (desiredAction0Index == NoActionIndex)
                {
                    if (currentAction0 != ActionIndexCache.act_none)
                        mountAgent.SetActionChannel(0, ActionIndexCache.act_none);
                }
                else if (currentAction0 == ActionIndexCache.act_none
                    || currentAction0.Index != desiredAction0Index
                    || nativeTurnFlagsChanged)
                {
                    mountAgent.SetActionChannel(
                        0,
                        new ActionIndexCache(desiredAction0Index),
                        ignorePriority: stationaryTurn,
                        additionalFlags: (AnimFlags)MountAction0Flag,
                        actionSpeed: MountAction0Speed,
                        startProgress: MountAction0Progress);
                }
                else
                {
                    mountAgent.SetCurrentActionProgress(0, MountAction0Progress);
                    mountAgent.SetCurrentActionSpeed(0, MountAction0Speed);
                }
            }

            //Currently not doing anything afaik
            if (mountAgent.GetCurrentAction(1) == ActionIndexCache.act_none || mountAgent.GetCurrentAction(1).Index != MountAction1Index)
            {
                string mActionName2 = AgentActionData.GetActionNameWithCode(MountAction1Index);
                if (mActionName2 != null)
                    mountAgent.SetActionChannel(1, ActionIndexCache.Create(mActionName2), additionalFlags: (AnimFlags)MountAction1Flag, startProgress: MountAction1Progress);
            }
            else
            {
                mountAgent.SetCurrentActionProgress(1, MountAction1Progress);
            }
            mountAgent.LookDirection = MountLookDirection;
            mountAgent.MovementInputVector = MountSpeed <= StationarySpeedThreshold && !stationaryTurn
                ? Vec2.Zero
                : MountInputVector;

            // Controller.None still lets native horse motion persist between replicated position corrections.
            // Cap that motion to the owner's real speed so a stopped owner also stops its puppet horse.
            mountAgent.SetMaximumSpeedLimit(MountSpeed, isMultiplier: false);
            Agent.MovementControlFlag movementFlags =
                (Agent.MovementControlFlag)MountMovementFlag;
            if (stationaryTurn && !syntheticStationaryTurn)
            {
                movementFlags = WithStationaryTurnMovementFlag(
                    movementFlags,
                    MountAction0TurnDirection);
            }
            AgentData.ApplyLocomotionMovementFlags(
                mountAgent,
                movementFlags);
        }

        internal static void GetRenderedAction0State(
            Agent mountAgent,
            out string animationName,
            out float animationSpeed)
        {
            GetRenderedAction0State(
                mountAgent,
                out animationName,
                out animationSpeed,
                out _);
        }

        internal static void GetRenderedAction0State(
            Agent mountAgent,
            out string animationName,
            out float animationSpeed,
            out float animationProgress)
        {
            animationName = null;
            animationSpeed = mountAgent == null ? 0f : 1f;
            animationProgress = mountAgent == null
                ? 0f
                : mountAgent.GetCurrentActionProgress(0);
            if (mountAgent == null) return;

            Skeleton skeleton = null;
            try
            {
                MBAgentVisuals visuals = mountAgent.AgentVisuals;
                if (ReferenceEquals(visuals, null) || !visuals.IsValid()) return;

                skeleton = visuals.GetSkeleton();
                if (ReferenceEquals(skeleton, null)) return;

                animationName = skeleton.GetAnimationAtChannel(0);
                float renderedSpeed = skeleton.GetAnimationSpeedAtChannel(0);
                animationSpeed = float.IsNaN(renderedSpeed) || float.IsInfinity(renderedSpeed)
                    ? 1f
                    : Math.Max(0f, renderedSpeed);
                float renderedProgress =
                    skeleton.GetAnimationParameterAtChannel(0);
                if (!float.IsNaN(renderedProgress)
                    && !float.IsInfinity(renderedProgress))
                {
                    animationProgress = Math.Max(
                        0f,
                        Math.Min(1f, renderedProgress));
                }
            }
            catch (NullReferenceException)
            {
                animationName = null;
                animationSpeed = 1f;
            }
            finally
            {
                if (!ReferenceEquals(skeleton, null))
                    skeleton.ManualInvalidate();
            }
        }

        internal static bool IsLocomotionAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return false;

            return animationName.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0
                || animationName.IndexOf("trot", StringComparison.OrdinalIgnoreCase) >= 0
                || animationName.IndexOf("canter", StringComparison.OrdinalIgnoreCase) >= 0
                || animationName.IndexOf("gallop", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsLocomotionAction(string actionName, string animationName)
        {
            return IsLocomotionAnimation(actionName)
                || IsLocomotionAnimation(animationName);
        }

        internal static bool IsLocomotionAction(int actionIndex, string animationName)
        {
            bool actionIsLocomotion = locomotionActionCache.GetOrAdd(
                actionIndex,
                IsLocomotionActionIndex);
            return actionIsLocomotion || IsLocomotionAnimation(animationName);
        }

        internal static int GetTurnDirection(string actionName, string animationName)
        {
            if (IsTurnDirection(actionName, "right") || IsTurnDirection(animationName, "right"))
                return TurnRight;
            if (IsTurnDirection(actionName, "left") || IsTurnDirection(animationName, "left"))
                return TurnLeft;

            return NoTurn;
        }

        internal static int GetTurnDirection(int actionIndex, string animationName)
        {
            TurnActionClassification actionTurn = turnActionCache.GetOrAdd(
                actionIndex,
                ClassifyTurnActionIndex);
            return actionTurn.Direction != NoTurn
                ? actionTurn.Direction
                : GetTurnDirection(null, animationName);
        }

        internal static int GetStationaryTurnDirection(int actionIndex)
        {
            TurnActionClassification actionTurn = turnActionCache.GetOrAdd(
                actionIndex,
                ClassifyTurnActionIndex);
            return actionTurn.IsStationary
                ? actionTurn.Direction
                : NoTurn;
        }

        internal static Agent.MovementControlFlag GetStationaryTurnMovementFlag(
            int turnDirection)
        {
            if (turnDirection == TurnRight)
                return Agent.MovementControlFlag.TurnRight;
            if (turnDirection == TurnLeft)
                return Agent.MovementControlFlag.TurnLeft;
            return Agent.MovementControlFlag.None;
        }

        internal static Agent.MovementControlFlag WithStationaryTurnMovementFlag(
            Agent.MovementControlFlag movementFlags,
            int turnDirection)
        {
            movementFlags &=
                ~(Agent.MovementControlFlag.TurnLeft | Agent.MovementControlFlag.TurnRight);
            return movementFlags | GetStationaryTurnMovementFlag(turnDirection);
        }

        internal static int GetTurnDirection(Vec2 previousDirection, Vec2 currentDirection)
        {
            if (!IsFinite(previousDirection)
                || !IsFinite(currentDirection)
                || previousDirection.LengthSquared <= 0.0001f
                || currentDirection.LengthSquared <= 0.0001f)
                return NoTurn;

            previousDirection.Normalize();
            currentDirection.Normalize();
            float dot = (previousDirection.X * currentDirection.X)
                + (previousDirection.Y * currentDirection.Y);
            if (dot >= 0.9999f)
                return NoTurn;

            float cross = (previousDirection.X * currentDirection.Y)
                - (previousDirection.Y * currentDirection.X);
            return cross > 0f ? TurnLeft : TurnRight;
        }

        internal static int ResolveAction0Index(
            int actionIndex,
            float speed,
            bool isLocomotion,
            int turnDirection,
            int turnActionIndex)
        {
            if (speed <= StationarySpeedThreshold && turnDirection != NoTurn)
                return turnActionIndex;
            if (actionIndex == NoActionIndex
                || (speed <= StationarySpeedThreshold && isLocomotion))
                return NoActionIndex;

            return actionIndex;
        }

        internal static string GetStationaryTurnActionName(
            string authoritativeActionName,
            string monsterUsage,
            int turnDirection)
        {
            if (IsStationaryTurnAction(authoritativeActionName))
                return authoritativeActionName;

            string mountType = string.Equals(
                monsterUsage,
                "camel",
                StringComparison.OrdinalIgnoreCase)
                ? "camel"
                : "horse";
            string direction = turnDirection == TurnRight ? "right" : "left";
            return $"act_{mountType}_turn_{direction}";
        }

        internal static bool IsStationaryTurnAction(string actionName)
        {
            return string.Equals(actionName, "act_horse_turn_right", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "act_horse_turn_left", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "act_camel_turn_right", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "act_camel_turn_left", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTurnDirection(string value, string direction)
        {
            if (string.IsNullOrEmpty(value)) return false;

            return value.IndexOf($"turn_{direction}", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf($"rotate_{direction}", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFinite(Vec2 value)
        {
            return !float.IsNaN(value.X)
                && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y)
                && !float.IsInfinity(value.Y);
        }

        private static TurnActionClassification ClassifyTurnActionIndex(int actionIndex)
        {
            string actionName = AgentActionData.GetActionNameWithCode(actionIndex);
            return new TurnActionClassification(
                GetTurnDirection(actionName, null),
                IsStationaryTurnAction(actionName));
        }

        private static int ResolveStationaryTurnActionIndex(
            int actionIndex,
            int turnDirection,
            string monsterUsage)
        {
            if (turnDirection == NoTurn)
                return NoActionIndex;
            TurnActionClassification actionTurn = turnActionCache.GetOrAdd(
                actionIndex,
                ClassifyTurnActionIndex);
            if (actionTurn.IsStationary)
                return actionIndex;

            return ActionIndexCache.Create(
                GetStationaryTurnActionName(
                    null,
                    monsterUsage,
                    turnDirection)).Index;
        }

        private static bool IsLocomotionActionIndex(int actionIndex)
        {
            return IsLocomotionAnimation(AgentActionData.GetActionNameWithCode(actionIndex));
        }

        private readonly struct TurnActionClassification
        {
            public TurnActionClassification(int direction, bool isStationary)
            {
                Direction = direction;
                IsStationary = isStationary;
            }

            public int Direction { get; }
            public bool IsStationary { get; }
        }

        [ProtoMember(1)]
        public Vec2 MountInputVector { get; }
        [ProtoMember(2)]
        public ulong MountAction1Flag { get; }
        [ProtoMember(3)]
        public float MountAction1Progress { get; }
        [ProtoMember(4)]
        public int MountAction1Index { get; }
        [ProtoMember(5)]
        public Vec3 MountLookDirection { get; }
        [ProtoMember(6)]
        public Vec2 MountMovementDirection { get; }
        [ProtoMember(7)]
        public Vec3 MountPosition { get; }
        /// <summary>The mount's owner-scoped movement id, or zero when the horse is unregistered.</summary>
        [ProtoMember(8)]
        public ushort MountMovementId { get; }
        [ProtoMember(9)]
        public ulong MountAction0Flag { get; }
        [ProtoMember(10)]
        public float MountAction0Progress { get; }
        [ProtoMember(11)]
        public int MountAction0Index { get; }
        /// <summary>The owner's horizontal mount speed, used as the puppet's absolute native speed limit.</summary>
        [ProtoMember(12)]
        public float MountSpeed { get; }
        /// <summary>Only populated when the mount's original owner differs from the rider's identity scope.</summary>
        [ProtoMember(13)]
        public string MountIdentityScopeId { get; }
        [ProtoMember(14)]
        public Guid MountAgentId { get; }
        [ProtoMember(15)]
        public uint MountMovementFlag { get; }
        /// <summary>The owner's rendered gait playback speed for action channel zero.</summary>
        [ProtoMember(16)]
        public float MountAction0Speed { get; }
        /// <summary>Whether the owner's rendered channel-zero animation is a locomotion gait.</summary>
        [ProtoMember(17)]
        public bool MountAction0IsLocomotion { get; }
        /// <summary>The owner's rendered stationary turn direction: -1 left, 0 none, 1 right.</summary>
        [ProtoMember(18)]
        public int MountAction0TurnDirection { get; }
        /// <summary>The native movement action for the owner's mount type and stationary turn direction.</summary>
        [ProtoMember(19)]
        public int MountAction0TurnActionIndex { get; }
        /// <summary>Whether channel zero is being driven through the bounded synthetic turn timeline.</summary>
        [ProtoMember(20)]
        public bool MountAction0IsSyntheticTurn { get; }
    }
}
