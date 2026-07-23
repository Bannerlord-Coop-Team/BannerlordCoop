using ProtoBuf;
using System;
using System.Collections.Concurrent;
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
        private static readonly ConcurrentDictionary<int, bool> locomotionActionCache =
            new ConcurrentDictionary<int, bool>();
        private static readonly ConcurrentDictionary<int, TurnActionClassification> turnActionCache =
            new ConcurrentDictionary<int, TurnActionClassification>();

        // The parameter is the MOUNT agent itself (callers pass rider.MountAgent), so read it directly —
        // mirroring ApplyMount. Dereferencing .MountAgent here was reading the mount's own (null) mount → NRE.
        public AgentMountData(
            Agent mountAgent,
            Guid mountId = default,
            float? mountAction0Speed = null,
            bool? mountAction0IsLocomotion = null,
            int? mountAction0TurnDirection = null,
            int? mountAction0TurnActionIndex = null)
        {
            MountInputVector = mountAgent.MovementInputVector;
            MountAction0Flag = (ulong)mountAgent.GetCurrentAnimationFlag(0);
            MountAction0Progress = mountAgent.GetCurrentActionProgress(0);
            MountAction0Index = mountAgent.GetCurrentAction(0).Index;
            string renderedAction0Animation = GetRenderedAction0Animation(mountAgent);
            MountAction1Flag = (ulong)mountAgent.GetCurrentAnimationFlag(1);
            MountAction1Progress = mountAgent.GetCurrentActionProgress(1);
            MountAction1Index = mountAgent.GetCurrentAction(1).Index;
            MountLookDirection = mountAgent.LookDirection;
            MountMovementDirection = mountAgent.GetMovementDirection();
            MountPosition = mountAgent.Position;
            MountSpeed = mountAgent.GetRealGlobalVelocity().AsVec2.Length;
            MountAction0Speed = mountAction0Speed ?? GetRenderedAction0Speed(mountAgent);
            MountAction0IsLocomotion = mountAction0IsLocomotion
                ?? IsLocomotionAction(
                    MountAction0Index,
                    renderedAction0Animation);
            MountAction0TurnDirection = mountAction0TurnDirection
                ?? GetTurnDirection(MountAction0Index, renderedAction0Animation);
            MountAction0TurnActionIndex = mountAction0TurnActionIndex
                ?? ResolveStationaryTurnActionIndex(
                    MountAction0Index,
                    MountAction0TurnDirection,
                    mountAgent.Monster?.MonsterUsage);
            MountId = mountId;
        }

        public void ApplyMount(Agent mountAgent)
        {
            // NOTE: mount position is NOT applied here — it is reconciled per-frame by AgentPositionInterpolator
            // (fed MountPosition by AgentMovementHandler). Everything below is per-packet mount state/animation.
            mountAgent.SetMovementDirection(MountMovementDirection);

            // Channel 0 is the horse's lower-body movement (stand, turn, or gait). A Controller.None puppet has
            // no controller to select it, so replicate the owner's rendered movement action explicitly.
            bool stationaryTurn = MountSpeed <= StationarySpeedThreshold
                && MountAction0TurnDirection != NoTurn;
            int desiredAction0Index = ResolveAction0Index(
                MountAction0Index,
                MountSpeed,
                MountAction0IsLocomotion,
                MountAction0TurnDirection,
                MountAction0TurnActionIndex);
            if (desiredAction0Index == ActionIndexCache.act_none.Index)
            {
                if (mountAgent.GetCurrentAction(0) != ActionIndexCache.act_none)
                    mountAgent.SetActionChannel(0, ActionIndexCache.act_none);
            }
            else if (mountAgent.GetCurrentAction(0) == ActionIndexCache.act_none || mountAgent.GetCurrentAction(0).Index != desiredAction0Index)
            {
                string movementActionName = AgentActionData.GetActionNameWithCode(desiredAction0Index);
                if (movementActionName != null)
                    mountAgent.SetActionChannel(
                        0,
                        ActionIndexCache.Create(movementActionName),
                        additionalFlags: (AnimFlags)MountAction0Flag,
                        actionSpeed: MountAction0Speed,
                        startProgress: MountAction0Progress);
            }
            else
            {
                mountAgent.SetCurrentActionProgress(0, MountAction0Progress);
                mountAgent.SetCurrentActionSpeed(0, MountAction0Speed);
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
        }

        internal static float GetRenderedAction0Speed(Agent mountAgent)
        {
            if (mountAgent == null) return 0f;

            TaleWorlds.Engine.Skeleton skeleton;
            try
            {
                skeleton = mountAgent.AgentVisuals?.GetSkeleton();
            }
            catch (NullReferenceException)
            {
                return 1f;
            }
            if (skeleton == null) return 1f;

            float animationSpeed = skeleton.GetAnimationSpeedAtChannel(0);
            return float.IsNaN(animationSpeed) || float.IsInfinity(animationSpeed)
                ? 1f
                : Math.Max(0f, animationSpeed);
        }

        private static string GetRenderedAction0Animation(Agent mountAgent)
        {
            try
            {
                return mountAgent?.AgentVisuals?.GetSkeleton()?.GetAnimationAtChannel(0);
            }
            catch (NullReferenceException)
            {
                return null;
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

        internal static int ResolveAction0Index(
            int actionIndex,
            float speed,
            bool isLocomotion,
            int turnDirection,
            int turnActionIndex)
        {
            if (speed <= StationarySpeedThreshold && turnDirection != NoTurn)
                return turnActionIndex;
            if (actionIndex == ActionIndexCache.act_none.Index
                || (speed <= StationarySpeedThreshold && isLocomotion))
                return ActionIndexCache.act_none.Index;

            return actionIndex;
        }

        private static bool IsTurnDirection(string value, string direction)
        {
            if (string.IsNullOrEmpty(value)) return false;

            return value.IndexOf($"turn_{direction}", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf($"rotate_{direction}", StringComparison.OrdinalIgnoreCase) >= 0;
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
                return ActionIndexCache.act_none.Index;
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

        private static bool IsLocomotionActionIndex(int actionIndex)
        {
            return IsLocomotionAnimation(AgentActionData.GetActionNameWithCode(actionIndex));
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
        /// <summary>The mount's own network id (registry id), or <see cref="Guid.Empty"/> when the horse isn't
        /// registered. Lets the receiver put the puppet on the EXACT horse the owner rides — including a
        /// mid-battle switch to a different horse — instead of guessing from the last one it dismounted.</summary>
        [ProtoMember(8)]
        public Guid MountId { get; }
        [ProtoMember(9)]
        public ulong MountAction0Flag { get; }
        [ProtoMember(10)]
        public float MountAction0Progress { get; }
        [ProtoMember(11)]
        public int MountAction0Index { get; }
        /// <summary>The owner's horizontal mount speed, used as the puppet's absolute native speed limit.</summary>
        [ProtoMember(12)]
        public float MountSpeed { get; }
        /// <summary>The owner's rendered gait playback speed for action channel zero.</summary>
        [ProtoMember(13)]
        public float MountAction0Speed { get; }
        /// <summary>Whether the owner's rendered channel-zero animation is a locomotion gait.</summary>
        [ProtoMember(14)]
        public bool MountAction0IsLocomotion { get; }
        /// <summary>The owner's rendered stationary turn direction: -1 left, 0 none, 1 right.</summary>
        [ProtoMember(15)]
        public int MountAction0TurnDirection { get; }
        /// <summary>The native movement action for the owner's mount type and stationary turn direction.</summary>
        [ProtoMember(16)]
        public int MountAction0TurnActionIndex { get; }
    }
}
