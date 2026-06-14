using ProtoBuf;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentActionData
    {
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

        public AgentActionData(Agent agent)
        {
            ActionIndexCache cache0 = agent.GetCurrentAction(0);
            ActionIndexCache cache1 = agent.GetCurrentAction(1);
            Agent.ActionCodeType actionTypeCh0 = agent.GetCurrentActionType(0);
            Agent.ActionCodeType actionTypeCh1 = agent.GetCurrentActionType(1);

            MovementFlag = (uint)agent.MovementFlags;
            EventFlag = (uint)agent.EventControlFlags;
            CrouchMode = agent.CrouchMode;

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
            // Set the agent's flags to 0 ie nothing
            agent.EventControlFlags = 0U;

            // if the agent is crouching, add that event
            if (CrouchMode)
            {

                agent.EventControlFlags |= Agent.EventControlFlag.Crouch;
            }
            else
            {
                agent.EventControlFlags |= Agent.EventControlFlag.Stand;
            }

            // Now check the flags given to us from the server
            uint eventFlag = EventFlag;
            if (eventFlag == 1u)
            {
                // dismount
                agent.EventControlFlags |= Agent.EventControlFlag.Dismount;
            }
            if (eventFlag == 2u)
            {
                // mount
                agent.EventControlFlags |= Agent.EventControlFlag.Mount;
            }
            if (eventFlag == 0x400u)
            {
                // switch weapon
                agent.EventControlFlags |= Agent.EventControlFlag.ToggleAlternativeWeapon;
            }

            // apply the animation on channel 0 if none exists
            if (agent.GetCurrentAction(0) == ActionIndexCache.act_none || agent.GetCurrentAction(0).Index != Action0Index)
            {
                string actionName1 = GetActionNameWithCode(Action0Index);
                if (actionName1 != null)
                    agent.SetActionChannel(0, ActionIndexCache.Create(actionName1), additionalFlags: (AnimFlags)Action0Flag, startProgress: Action0Progress);

            }
            // otherwise continue the existing animation
            else
            {
                agent.SetCurrentActionProgress(0, Action0Progress);
            }

            // Set the movement flags to none
            agent.MovementFlags = 0U;

            // Check the action of the agent; if they are defending, apply the defending movement flag
            if (Action1CodeType >= (int)Agent.ActionCodeType.DefendAllBegin && Action1CodeType <= (int)Agent.ActionCodeType.DefendAllEnd)

            {
                agent.MovementFlags = (Agent.MovementControlFlag)MovementFlag;
                return;
            }


            // Check if there is a melee; this breaks the game if we don't do it.
            if ((Agent.ActionCodeType)Action1CodeType != Agent.ActionCodeType.BlockedMelee)
            {
                // if the animation is none, start it
                if (agent.GetCurrentAction(1) == ActionIndexCache.act_none || agent.GetCurrentAction(1).Index != Action1Index)
                {
                    string actionName2 = GetActionNameWithCode(Action1Index);
                    if (actionName2 != null)
                        agent.SetActionChannel(1, ActionIndexCache.Create(actionName2), additionalFlags: (AnimFlags)Action1Flag, startProgress: Action1Progress);

                }
                // otherwise continue it
                else
                {
                    agent.SetCurrentActionProgress(1, Action1Progress);
                }
            }
            else
            {
                // otherwise just cancel it
                agent.SetActionChannel(1, ActionIndexCache.act_none, ignorePriority: true, startProgress: 100);
            }
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
    }
}
