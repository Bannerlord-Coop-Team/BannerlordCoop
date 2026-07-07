using ProtoBuf;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentActionData
    {
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
            agent.EventControlFlags |= (Agent.EventControlFlag)EventFlag;
            agent.MovementFlags = (Agent.MovementControlFlag)MovementFlag;

            ApplyActionChannel(agent, 0, Action0Index, Action0Flag, Action0Progress, updateProgress: true);
            ApplyActionChannel(agent, 1, Action1Index, Action1Flag, Action1Progress, updateProgress: true);

            // Set the movement flags to none
            agent.MovementFlags = 0U;

            // Check the action of the agent; if they are defending, apply the defending movement flag
            if (Action1CodeType >= (int)Agent.ActionCodeType.DefendAllBegin && Action1CodeType <= (int)Agent.ActionCodeType.DefendAllEnd)
            {
                agent.MovementFlags = (Agent.MovementControlFlag)MovementFlag;
                return;
            }


            //// Check if there is a melee; this breaks the game if we don't do it.
            //if ((Agent.ActionCodeType)Action1CodeType != Agent.ActionCodeType.BlockedMelee)
            //{
            //    // if the animation is none, start it
            //    if (agent.GetCurrentAction(1) == ActionIndexCache.act_none || agent.GetCurrentAction(1).Index != Action1Index)
            //    {
            //        string actionName2 = GetActionNameWithCode(Action1Index);
            //        if (actionName2 != null)
            //            agent.SetActionChannel(1, ActionIndexCache.Create(actionName2), additionalFlags: (AnimFlags)Action1Flag, startProgress: Action1Progress);

            //    }
            //    // otherwise continue it
            //    else
            //    {
            //        agent.SetCurrentActionProgress(1, Action1Progress);
            //    }
            //}
            //else
            //{
            //    // otherwise just cancel it
            //    agent.SetActionChannel(1, ActionIndexCache.act_none, ignorePriority: true, startProgress: 100);
            //}
        }

        public void ApplyMovementActions(Agent agent)
        {
            ApplyMovementActionChannel(agent, 0, Action0CodeType, Action0Index, Action0Flag, Action0Progress);
            ApplyMovementActionChannel(agent, 1, Action1CodeType, Action1Index, Action1Flag, Action1Progress);
        }

        internal static bool IsMovementAction(Agent.ActionCodeType type)
        {
            return type == Agent.ActionCodeType.Other || type == Agent.ActionCodeType.Idle;
        }

        internal static void ApplyActionChannel(
            Agent agent,
            int channelNo,
            int actionIndex,
            ulong actionFlag,
            float actionProgress,
            bool updateProgress)
        {
            if (agent.GetCurrentAction(channelNo).Index != actionIndex)
            {
                agent.SetActionChannel(
                    channelNo,
                    new ActionIndexCache(actionIndex),
                    additionalFlags: (AnimFlags)actionFlag,
                    startProgress: actionProgress);
            }
            else if (updateProgress)
            {
                agent.SetCurrentActionProgress(channelNo, actionProgress);
            }
        }

        private static void ApplyMovementActionChannel(
            Agent agent,
            int channelNo,
            int actionCodeType,
            int actionIndex,
            ulong actionFlag,
            float actionProgress)
        {
            if (!IsMovementAction((Agent.ActionCodeType)actionCodeType))
                return;

            if (!IsMovementAction(agent.GetCurrentActionType(channelNo)))
                return;

            ApplyActionChannel(agent, channelNo, actionIndex, actionFlag, actionProgress, updateProgress: false);
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
