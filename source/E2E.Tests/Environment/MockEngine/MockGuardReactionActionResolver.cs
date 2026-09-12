using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

public sealed class MockGuardReactionActionResolver :
    IGuardReactionActionResolver
{
    public bool TryResolve(
        Agent agent,
        out int channel,
        out ActionIndexCache reactionAction,
        out AnimFlags animationFlags)
    {
        for (int candidate = 1; candidate >= 0; candidate--)
        {
            ActionIndexCache guardAction =
                agent.GetCurrentAction(candidate);
            if (guardAction == ActionIndexCache.act_none
                || !AgentActionData.IsDefendingAction(
                    agent.GetCurrentActionType(candidate)))
            {
                continue;
            }

            channel = candidate;
            reactionAction =
                new ActionIndexCache(guardAction.Index + 2);
            animationFlags = AnimFlags.amf_priority_defend;
            return true;
        }

        channel = -1;
        reactionAction = ActionIndexCache.act_none;
        animationFlags = (AnimFlags)0uL;
        return false;
    }
}
