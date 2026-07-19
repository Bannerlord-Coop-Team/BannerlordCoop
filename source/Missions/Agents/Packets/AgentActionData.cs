using ProtoBuf;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentActionData
    {
        internal const Agent.MovementControlFlag DefendMovementFlagsMask =
            Agent.MovementControlFlag.DefendMask | Agent.MovementControlFlag.DefendBlock;

        // MBAPI.IMBAnimation is a non-public static field. The publicizer makes it compile, but the
        // emitted IgnoresAccessChecksTo isn't honored in every runtime load context (it throws
        // FieldAccessException in live play). Reflecting a non-public static field always works, so
        // resolve action names through here instead of touching MBAPI.IMBAnimation directly.
        private static readonly FieldInfo AnimationField =
            typeof(MBAPI).GetField("IMBAnimation", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static MethodInfo getActionNameWithCode;

        internal static string GetActionNameWithCode(int actionCode)
        {
            var animation = AnimationField?.GetValue(null);
            if (animation == null) return null;

            if (getActionNameWithCode == null)
            {
                getActionNameWithCode = animation.GetType().GetMethod("GetActionNameWithCode", new[] { typeof(int) });
            }

            return getActionNameWithCode?.Invoke(animation, new object[] { actionCode }) as string;
        }

        internal static Agent.MovementControlFlag GetDefendMovementFlags(
            Agent.MovementControlFlag movementFlags)
        {
            return movementFlags & DefendMovementFlagsMask;
        }

        internal static Agent.MovementControlFlag GetEffectiveDefendMovementFlags(
            Agent agent)
        {
            Agent.MovementControlFlag defendFlags =
                GetDefendMovementFlags(agent.MovementFlags);
            if (defendFlags != Agent.MovementControlFlag.None || !agent.HasMount)
                return defendFlags;

            Agent.ActionCodeType action0Type = agent.GetCurrentActionType(0);
            Agent.ActionCodeType action1Type = agent.GetCurrentActionType(1);
            if (!IsDefendingAction(action0Type) && !IsDefendingAction(action1Type))
                return Agent.MovementControlFlag.None;

            // Mounted guards can outlive the frame's defend flags, so recompute them while the action is active.
            defendFlags = GetDefendMovementFlags(agent.GetDefendMovementFlag());
            if (defendFlags != Agent.MovementControlFlag.None)
                return defendFlags;

            // Directionless guard actions still need held defend input on the puppet.
            return Agent.MovementControlFlag.DefendBlock;
        }

        internal static void ApplyDefendMovementFlags(
            Agent agent,
            Agent.MovementControlFlag defendFlags)
        {
            Agent.MovementControlFlag movementFlags =
                agent.MovementFlags & ~DefendMovementFlagsMask;
            agent.MovementFlags = movementFlags | GetDefendMovementFlags(defendFlags);
        }

        internal static void ApplyGuardState(Agent agent, Agent.GuardMode guardMode)
        {
            if (IsGuardMode(guardMode))
            {
                agent.SetWeaponGuard(GuardModeToUsageDirection(guardMode));
                return;
            }

            if (guardMode == Agent.GuardMode.None && IsGuardMode(agent.CurrentGuardMode))
            {
                agent.ResetGuard();
            }
        }

        internal static bool IsGuardMode(Agent.GuardMode guardMode) =>
            guardMode == Agent.GuardMode.Up
            || guardMode == Agent.GuardMode.Down
            || guardMode == Agent.GuardMode.Left
            || guardMode == Agent.GuardMode.Right;

        internal static Agent.GuardMode GetGuardModeFromDefendFlags(
            Agent.MovementControlFlag defendFlags)
        {
            if ((defendFlags & Agent.MovementControlFlag.DefendDown) != 0)
                return Agent.GuardMode.Down;
            if ((defendFlags & Agent.MovementControlFlag.DefendUp) != 0)
                return Agent.GuardMode.Up;
            if ((defendFlags & Agent.MovementControlFlag.DefendLeft) != 0)
                return Agent.GuardMode.Left;
            if ((defendFlags & Agent.MovementControlFlag.DefendRight) != 0)
                return Agent.GuardMode.Right;

            return Agent.GuardMode.None;
        }

        internal static Agent.GuardMode GetEffectiveGuardMode(
            Agent agent,
            Agent.MovementControlFlag defendFlags)
        {
            Agent.GuardMode guardMode = agent.CurrentGuardMode;
            if (IsGuardMode(guardMode))
                return guardMode;

            if (defendFlags == Agent.MovementControlFlag.None)
                return Agent.GuardMode.None;

            guardMode = GetGuardModeFromDefendFlags(defendFlags);
            if (IsGuardMode(guardMode))
                return guardMode;

            // Mounted shields keep their exact direction on the defend action even when CurrentGuardMode is unset.
            guardMode = GetGuardModeFromDefendDirection(
                agent.GetCurrentActionDirection(1));
            if (IsGuardMode(guardMode))
                return guardMode;

            return GetGuardModeFromDefendDirection(
                agent.GetCurrentActionDirection(0));
        }

        private static bool IsDefendingAction(Agent.ActionCodeType actionType)
        {
            return (actionType >= Agent.ActionCodeType.DefendAllBegin
                    && actionType < Agent.ActionCodeType.DefendAllEnd)
                || actionType == Agent.ActionCodeType.Guard;
        }

        private static Agent.GuardMode GetGuardModeFromDefendDirection(
            Agent.UsageDirection direction) =>
            direction switch
            {
                Agent.UsageDirection.DefendUp => Agent.GuardMode.Up,
                Agent.UsageDirection.DefendDown => Agent.GuardMode.Down,
                Agent.UsageDirection.DefendLeft => Agent.GuardMode.Left,
                Agent.UsageDirection.DefendRight => Agent.GuardMode.Right,
                _ => Agent.GuardMode.None
            };

        private static Agent.UsageDirection GuardModeToUsageDirection(
            Agent.GuardMode guardMode) =>
            guardMode switch
            {
                Agent.GuardMode.Up => Agent.UsageDirection.AttackUp,
                Agent.GuardMode.Down => Agent.UsageDirection.AttackDown,
                Agent.GuardMode.Left => Agent.UsageDirection.AttackLeft,
                Agent.GuardMode.Right => Agent.UsageDirection.AttackRight,
                _ => Agent.UsageDirection.None
            };

        private static int ToWireGuardState(Agent.GuardMode guardMode) =>
            IsGuardMode(guardMode) ? (int)guardMode + 1 : 0;

        private static Agent.GuardMode FromWireGuardState(int guardState) =>
            guardState > 0 ? (Agent.GuardMode)(guardState - 1) : Agent.GuardMode.None;

        public AgentActionData(Agent agent)
            : this(agent, GetEffectiveDefendMovementFlags(agent))
        {
        }

        private AgentActionData(
            Agent agent,
            Agent.MovementControlFlag defendFlags)
            : this(
                agent,
                defendFlags,
                GetEffectiveGuardMode(agent, defendFlags))
        {
        }

        internal AgentActionData(
            Agent agent,
            Agent.MovementControlFlag defendFlags,
            Agent.GuardMode guardMode)
        {
            ActionIndexCache cache0 = agent.GetCurrentAction(0);
            ActionIndexCache cache1 = agent.GetCurrentAction(1);

            Agent.MovementControlFlag movementFlags = agent.MovementFlags;
            movementFlags &= ~DefendMovementFlagsMask;
            movementFlags |= defendFlags;

            MovementFlag = (uint)movementFlags;
            EventFlag = (uint)agent.EventControlFlags;
            CrouchMode = agent.CrouchMode;
            GuardState = ToWireGuardState(guardMode);

            Action0Index = cache0.Index;
            Action0Progress = agent.GetCurrentActionProgress(0);
            Action0Flag = (ulong)agent.GetCurrentAnimationFlag(0);
            Action1Index = cache1.Index;
            Action1Progress = agent.GetCurrentActionProgress(1);
            Action1Flag = (ulong)agent.GetCurrentAnimationFlag(1);
        }

        public void Apply(Agent agent)
        {
            Agent.MovementControlFlag movementFlags = (Agent.MovementControlFlag)MovementFlag;
            agent.EventControlFlags |= (Agent.EventControlFlag)EventFlag;
            agent.MovementFlags = movementFlags;

            // apply the animation on channel 0 if none exists
            if (agent.GetCurrentAction(0) == ActionIndexCache.act_none || agent.GetCurrentAction(0).Index != Action0Index)
            {
                // Use the reflection helper, NOT MBAPI.IMBAnimation directly: the publicized static field
                // throws FieldAccessException in live play (see GetActionNameWithCode above), which kills
                // every movement-packet apply and leaves remote agents frozen.
                string actionName1 = GetActionNameWithCode(Action0Index);
                if (actionName1 != null)
                {
                    agent.SetActionChannel(0, ActionIndexCache.Create(actionName1), additionalFlags: (AnimFlags)Action0Flag, startProgress: Action0Progress);
                }
            }
            // otherwise continue the existing animation
            else
            {
                agent.SetCurrentActionProgress(0, Action0Progress);
            }

            // apply the animation on channel 1 if none exists
            if (agent.GetCurrentAction(1) == ActionIndexCache.act_none || agent.GetCurrentAction(1).Index != Action1Index)
            {
                string actionName2 = GetActionNameWithCode(Action1Index);
                if (actionName2 != null)
                {
                    agent.SetActionChannel(1, ActionIndexCache.Create(actionName2), additionalFlags: (AnimFlags)Action1Flag, startProgress: Action1Progress);
                }
            }
            // otherwise continue the existing animation
            else
            {
                agent.SetCurrentActionProgress(1, Action1Progress);
            }

            // Keep held defend input on the puppet; later reliable transitions replace or clear these bits.
            agent.MovementFlags = GetDefendMovementFlags(movementFlags);
        }

        [ProtoMember(1)]
        public float Action0Progress { get; }
        [ProtoMember(2)]
        public ulong Action0Flag { get; }
        [ProtoMember(3)]
        public int Action0Index { get; }
        [ProtoMember(4)]
        public float Action1Progress { get; }
        [ProtoMember(5)]
        public ulong Action1Flag { get; }
        [ProtoMember(6)]
        public int Action1Index { get; }
        [ProtoMember(7)]
        public uint MovementFlag { get; }
        [ProtoMember(8)]
        public uint EventFlag { get; }
        [ProtoMember(9)]
        public bool CrouchMode { get; }
        [ProtoMember(10)]
        public int GuardState { get; }
        internal Agent.MovementControlFlag DefendFlags =>
            GetDefendMovementFlags((Agent.MovementControlFlag)MovementFlag);
        internal Agent.GuardMode GuardMode => FromWireGuardState(GuardState);
    }
}
