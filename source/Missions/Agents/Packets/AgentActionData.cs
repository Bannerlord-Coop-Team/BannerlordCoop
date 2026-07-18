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
        {
            ActionIndexCache cache0 = agent.GetCurrentAction(0);
            ActionIndexCache cache1 = agent.GetCurrentAction(1);
            Agent.ActionCodeType actionTypeCh0 = agent.GetCurrentActionType(0);
            Agent.ActionCodeType actionTypeCh1 = agent.GetCurrentActionType(1);

            MovementFlag = (uint)agent.MovementFlags;
            EventFlag = (uint)agent.EventControlFlags;
            CrouchMode = agent.CrouchMode;
            GuardState = ToWireGuardState(agent.CurrentGuardMode);

            Action0CodeType = (int)actionTypeCh0;
            Action0Index = cache0.Index;
            Action0Progress = agent.GetCurrentActionProgress(0);
            Action0Flag = (ulong)agent.GetCurrentAnimationFlag(0);
            Action1CodeType = (int)actionTypeCh1;
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
        public int Action0CodeType { get; }
        [ProtoMember(5)]
        public float Action1Progress { get; }
        [ProtoMember(6)]
        public ulong Action1Flag { get; }
        [ProtoMember(7)]
        public int Action1Index { get; }
        [ProtoMember(8)]
        public int Action1CodeType { get; }
        [ProtoMember(9)]
        public uint MovementFlag { get; }
        [ProtoMember(10)]
        public uint EventFlag { get; }
        [ProtoMember(11)]
        public bool CrouchMode { get; }
        [ProtoMember(12)]
        public int GuardState { get; }
        internal Agent.MovementControlFlag DefendFlags =>
            GetDefendMovementFlags((Agent.MovementControlFlag)MovementFlag);
        internal Agent.GuardMode GuardMode => FromWireGuardState(GuardState);
    }
}
