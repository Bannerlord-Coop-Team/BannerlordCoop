using ProtoBuf;
using Missions.Agents.Handlers;
using System.Reflection;
using TaleWorlds.Core;
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
            if (defendFlags != Agent.MovementControlFlag.None)
                return defendFlags;

            if (agent.HasMount)
            {
                defendFlags = GetDefendMovementFlags(
                    agent.GetDefendMovementFlag());
                // Mounted aim reports a defend direction even while idle. DefendBlock is the held-input bit.
                if ((defendFlags & Agent.MovementControlFlag.DefendBlock) != 0)
                    return defendFlags;
            }

            Agent.ActionCodeType action0Type = agent.GetCurrentActionType(0);
            Agent.ActionCodeType action1Type = agent.GetCurrentActionType(1);
            if (!IsDefendingAction(action0Type) && !IsDefendingAction(action1Type))
                return Agent.MovementControlFlag.None;

            // Guard actions can outlive the frame's defend flags, so recompute them while the action is active.
            defendFlags = GetDefendMovementFlags(agent.GetDefendMovementFlag());
            if (defendFlags != Agent.MovementControlFlag.None)
                return defendFlags;

            // Mounted directionless guards still need held defend input on the puppet. On foot, a lingering
            // release animation must not synthesize a fresh block after native input has cleared.
            return agent.HasMount
                ? Agent.MovementControlFlag.DefendBlock
                : Agent.MovementControlFlag.None;
        }

        internal static void ApplyDefendMovementFlags(
            Agent agent,
            Agent.MovementControlFlag defendFlags)
        {
            Agent.MovementControlFlag movementFlags =
                agent.MovementFlags & ~DefendMovementFlagsMask;
            agent.MovementFlags = movementFlags | GetDefendMovementFlags(defendFlags);
        }

        internal static void ApplyGuardState(
            Agent agent,
            Agent.GuardMode guardMode,
            bool force = false)
        {
            if (IsGuardMode(guardMode))
            {
                if (force || agent.CurrentGuardMode != guardMode)
                    agent.SetWeaponGuard(GuardModeToUsageDirection(guardMode));
                return;
            }

            if (guardMode == Agent.GuardMode.None && IsGuardMode(agent.CurrentGuardMode))
            {
                agent.ResetGuard();
            }
        }

        internal static void ApplyGuardDirectionTransition(
            Agent agent,
            Agent.GuardMode guardMode)
        {
            if (!IsGuardMode(guardMode))
                return;

            agent.ResetGuard();
            ApplyGuardState(agent, guardMode, force: true);
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
            if (!agent.HasMount && IsGuardMode(guardMode))
                return guardMode;

            if (defendFlags == Agent.MovementControlFlag.None)
            {
                return IsGuardMode(guardMode)
                    ? guardMode
                    : Agent.GuardMode.None;
            }

            if (agent.HasMount)
            {
                guardMode = GetGuardModeFromDefendingAction(agent);
                if (IsGuardMode(guardMode))
                    return guardMode;
            }

            guardMode = agent.CurrentGuardMode;
            if (IsGuardMode(guardMode))
                return guardMode;

            guardMode = GetGuardModeFromDefendFlags(defendFlags);
            if (IsGuardMode(guardMode))
                return guardMode;

            guardMode = GetGuardModeFromDefendDirection(
                agent.GetCurrentActionDirection(1));
            if (IsGuardMode(guardMode))
                return guardMode;

            return GetGuardModeFromDefendDirection(
                agent.GetCurrentActionDirection(0));
        }

        internal static Agent.MovementControlFlag AlignDefendDirection(
            Agent.MovementControlFlag defendFlags,
            Agent.GuardMode guardMode)
        {
            if (defendFlags == Agent.MovementControlFlag.None ||
                !IsGuardMode(guardMode))
            {
                return defendFlags;
            }

            return (defendFlags &
                    ~Agent.MovementControlFlag.DefendDirMask) |
                GuardModeToDefendFlag(guardMode);
        }

        internal static bool IsDefendingAction(Agent.ActionCodeType actionType)
        {
            return (actionType >= Agent.ActionCodeType.DefendAllBegin
                    && actionType < Agent.ActionCodeType.DefendAllEnd)
                || actionType == Agent.ActionCodeType.Guard;
        }

        internal static bool IsGuardPresentationAction(
            Agent.ActionCodeType actionType)
        {
            return IsDefendingAction(actionType)
                || IsGuardReactionAction(actionType);
        }

        internal static bool IsGuardReactionAction(
            Agent.ActionCodeType actionType)
        {
            return actionType == Agent.ActionCodeType.ParriedMelee
                || actionType == Agent.ActionCodeType.BlockedMelee;
        }

        private static int GetGuardPresentationChannel(Agent agent)
        {
            if (!agent.HasMount)
                return -1;

            Agent.ActionCodeType action1Type = agent.GetCurrentActionType(1);
            Agent.ActionCodeType action0Type = agent.GetCurrentActionType(0);
            if (IsGuardReactionAction(action1Type))
                return 1;
            if (IsGuardReactionAction(action0Type))
                return 0;
            if (IsDefendingAction(action1Type))
                return 1;
            if (IsDefendingAction(action0Type))
                return 0;

            return -1;
        }

        private static int GetGuardActionChannel(
            Agent agent,
            int guardPresentationChannel)
        {
            if (guardPresentationChannel >= 0)
                return guardPresentationChannel;

            if (IsDefendingAction(agent.GetCurrentActionType(1)))
                return 1;
            if (IsDefendingAction(agent.GetCurrentActionType(0)))
                return 0;

            return -1;
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

        internal static Agent.GuardMode GetGuardModeFromDefendingAction(
            Agent agent)
        {
            Agent.GuardMode guardMode =
                GetGuardModeFromDefendingAction(agent, 1);
            return IsGuardMode(guardMode)
                ? guardMode
                : GetGuardModeFromDefendingAction(agent, 0);
        }

        private static Agent.GuardMode GetGuardModeFromDefendingAction(
            Agent agent,
            int channel)
        {
            if (!IsDefendingAction(agent.GetCurrentActionType(channel)))
                return Agent.GuardMode.None;

            return GetGuardModeFromDefendDirection(
                agent.GetCurrentActionDirection(channel));
        }

        private static Agent.MovementControlFlag GuardModeToDefendFlag(
            Agent.GuardMode guardMode) =>
            guardMode switch
            {
                Agent.GuardMode.Up =>
                    Agent.MovementControlFlag.DefendUp,
                Agent.GuardMode.Down =>
                    Agent.MovementControlFlag.DefendDown,
                Agent.GuardMode.Left =>
                    Agent.MovementControlFlag.DefendLeft,
                Agent.GuardMode.Right =>
                    Agent.MovementControlFlag.DefendRight,
                _ => Agent.MovementControlFlag.None
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
            Agent.GuardMode guardMode,
            int guardReactionChannel = -1)
        {
            ActionIndexCache cache0 = agent.GetCurrentAction(0);
            ActionIndexCache cache1 = agent.GetCurrentAction(1);

            if (agent.HasMount)
            {
                defendFlags = AlignDefendDirection(
                    defendFlags,
                    guardMode);
            }

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
            int validGuardReactionChannel =
                guardReactionChannel >= 0
                && guardReactionChannel <= 1
                    ? guardReactionChannel
                    : -1;
            GuardPresentationChannel =
                agent.HasMount && validGuardReactionChannel >= 0
                    ? validGuardReactionChannel
                    : GetGuardPresentationChannel(agent);
            GuardActionChannel =
                validGuardReactionChannel >= 0
                    ? validGuardReactionChannel
                    : GetGuardActionChannel(
                        agent,
                        GuardPresentationChannel);
            GuardActionIsDefending =
                GuardActionChannel >= 0
                && IsDefendingAction(
                    agent.GetCurrentActionType(
                        GuardActionChannel));
            GuardActionIsReaction =
                GuardActionChannel >= 0
                && (guardReactionChannel == GuardActionChannel
                    || IsGuardReactionAction(
                        agent.GetCurrentActionType(
                            GuardActionChannel)));
            IsMounted = agent.HasMount;
            IsPlayerControlled =
                agent.Controller == AgentControllerType.Player;
        }

        public void Apply(
            Agent agent,
            IAgentVisualActionAccessor visualActionAccessor)
        {
            Agent.MovementControlFlag movementFlags = (Agent.MovementControlFlag)MovementFlag;
            agent.EventControlFlags |= (Agent.EventControlFlag)EventFlag;
            ApplyDefendMovementFlags(agent, movementFlags);

            // Install action transitions, but let an unchanged native action advance on its local timeline.
            if (NeedsActionTransition(
                agent,
                0,
                Action0Index,
                visualActionAccessor,
                preserveVisibleAction:
                    IsMounted && GuardActionChannel == 0,
                preserveCurrentGuardReaction:
                    ShouldPreserveCurrentGuardReaction(
                        agent,
                        0)))
            {
                if (TryResolveActionTransition(
                    agent,
                    0,
                    Action0Index,
                    out ActionIndexCache action0))
                {
                    agent.SetActionChannel(
                        0,
                        action0,
                        ignorePriority:
                            ShouldForceMountedGuardDirectionTransition(
                                agent,
                                0),
                        additionalFlags: (AnimFlags)Action0Flag,
                        startProgress: Action0Progress);
                }
            }

            if (NeedsActionTransition(
                agent,
                1,
                Action1Index,
                visualActionAccessor,
                preserveVisibleAction:
                    IsMounted && GuardActionChannel == 1,
                preserveCurrentGuardReaction:
                    ShouldPreserveCurrentGuardReaction(
                        agent,
                        1)))
            {
                if (TryResolveActionTransition(
                    agent,
                    1,
                    Action1Index,
                    out ActionIndexCache action1))
                {
                    agent.SetActionChannel(
                        1,
                        action1,
                        ignorePriority:
                            ShouldForceMountedGuardDirectionTransition(
                                agent,
                                1),
                        additionalFlags: (AnimFlags)Action1Flag,
                        startProgress: Action1Progress);
                }
            }

            // Keep held defend input on the puppet; later reliable transitions replace or clear these bits.
            ApplyDefendMovementFlags(agent, movementFlags);
        }

        private bool TryResolveActionTransition(
            Agent agent,
            int channel,
            int actionIndex,
            out ActionIndexCache action)
        {
            string actionName = GetActionNameWithCode(actionIndex);
            if (actionName != null)
            {
                action = ActionIndexCache.Create(actionName);
                return true;
            }

            Agent.GuardMode currentActionGuardMode =
                GetGuardModeFromDefendDirection(
                    agent.GetCurrentActionDirection(channel));
            if (actionIndex >= 0
                && IsMounted
                && GuardActionChannel == channel
                && IsGuardMode(GuardMode)
                && IsGuardMode(currentActionGuardMode)
                && currentActionGuardMode != GuardMode)
            {
                // The synchronized native index is sufficient when a stale sibling guard must transition.
                action = new ActionIndexCache(actionIndex);
                return true;
            }

            action = new ActionIndexCache(-1);
            return false;
        }

        private bool ShouldForceMountedGuardDirectionTransition(
            Agent agent,
            int channel)
        {
            if (!IsMounted
                || GuardActionChannel != channel
                || !IsGuardMode(GuardMode))
            {
                return false;
            }

            Agent.GuardMode currentActionGuardMode =
                GetGuardModeFromDefendDirection(
                    agent.GetCurrentActionDirection(channel));
            // Equal-priority mounted guard siblings can reject this one-shot transition.
            return IsGuardMode(currentActionGuardMode)
                && currentActionGuardMode != GuardMode;
        }

        private static bool NeedsActionTransition(
            Agent agent,
            int channel,
            int expectedActionIndex,
            IAgentVisualActionAccessor visualActionAccessor,
            bool preserveVisibleAction,
            bool preserveCurrentGuardReaction)
        {
            if (preserveCurrentGuardReaction)
                return false;

            ActionIndexCache currentAction =
                agent.GetCurrentAction(channel);
            if (currentAction != ActionIndexCache.act_none)
                return currentAction.Index != expectedActionIndex;
            if (expectedActionIndex == ActionIndexCache.act_none.Index)
                return false;
            if (!preserveVisibleAction)
                return true;

            ActionIndexCache expectedAction =
                new ActionIndexCache(expectedActionIndex);
            return !visualActionAccessor.IsActionVisible(
                agent,
                channel,
                in expectedAction);
        }

        internal bool ShouldPreserveCurrentGuardReaction(
            Agent agent,
            int channel)
        {
            if (GuardActionIsReaction
                || GuardActionChannel != channel
                || !GuardActionIsDefending
                || (DefendFlags == Agent.MovementControlFlag.None
                    && !IsGuardMode(GuardMode)))
            {
                return false;
            }

            Agent.ActionCodeType actionType =
                agent.GetCurrentActionType(channel);
            if (IsGuardReactionAction(actionType))
                return true;

            Agent.GuardMode currentActionGuardMode =
                GetGuardModeFromDefendDirection(
                    agent.GetCurrentActionDirection(channel));
            if (IsGuardMode(GuardMode)
                && IsGuardMode(currentActionGuardMode)
                && currentActionGuardMode != GuardMode)
            {
                return false;
            }

            return IsDefendingAction(actionType)
                && agent.GetCurrentActionStage(channel)
                    == Agent.ActionStage.DefendParry;
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
        [ProtoMember(11)]
        public int GuardPresentationChannel { get; }
        [ProtoMember(12)]
        public bool IsMounted { get; }
        [ProtoMember(13)]
        public int GuardActionChannel { get; }
        [ProtoMember(14)]
        public bool GuardActionIsDefending { get; }
        [ProtoMember(15)]
        public bool IsPlayerControlled { get; }
        [ProtoMember(16)]
        public bool GuardActionIsReaction { get; }
        internal Agent.MovementControlFlag DefendFlags =>
            GetDefendMovementFlags((Agent.MovementControlFlag)MovementFlag);
        internal Agent.GuardMode GuardMode => FromWireGuardState(GuardState);
    }
}
