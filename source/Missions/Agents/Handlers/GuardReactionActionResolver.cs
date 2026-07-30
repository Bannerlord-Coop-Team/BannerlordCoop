using Missions.Agents.Packets;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public interface IGuardReactionActionResolver
{
    bool TryResolve(
        Agent agent,
        out int channel,
        out ActionIndexCache reactionAction,
        out AnimFlags animationFlags);
}

public sealed class GuardReactionActionResolver :
    IGuardReactionActionResolver
{
    private const string PassiveToken = "_passive";
    private const string ActiveToken = "_active";
    private const string LightParryToken = "_parry_light";

    public bool TryResolve(
        Agent agent,
        out int channel,
        out ActionIndexCache reactionAction,
        out AnimFlags animationFlags)
    {
        if (agent != null)
        {
            for (int candidate = 1; candidate >= 0; candidate--)
            {
                if (TryResolve(
                        agent,
                        candidate,
                        out reactionAction))
                {
                    channel = candidate;
                    animationFlags =
                        AnimFlags.amf_priority_defend;
                    return true;
                }
            }
        }

        channel = -1;
        reactionAction = ActionIndexCache.act_none;
        animationFlags = (AnimFlags)0uL;
        return false;
    }

    private static bool TryResolve(
        Agent agent,
        int candidateChannel,
        out ActionIndexCache reactionAction)
    {
        reactionAction = ActionIndexCache.act_none;
        if (agent == null
            || !AgentActionData.IsDefendingAction(
                agent.GetCurrentActionType(candidateChannel)))
        {
            return false;
        }

        ActionIndexCache guardAction =
            agent.GetCurrentAction(candidateChannel);
        if (guardAction == ActionIndexCache.act_none)
            return false;

        string reactionActionName = GetParryLightActionName(
            AgentActionData.GetActionNameWithCode(
                guardAction.Index));
        if (reactionActionName == null)
            return false;

        reactionAction =
            ActionIndexCache.Create(reactionActionName);
        return reactionAction.Index >= 0;
    }

    internal static string GetParryLightActionName(
        string guardActionName)
    {
        if (string.IsNullOrEmpty(guardActionName))
            return null;

        int tokenIndex = guardActionName.IndexOf(
            PassiveToken,
            StringComparison.Ordinal);
        string token = PassiveToken;
        if (tokenIndex < 0)
        {
            tokenIndex = guardActionName.IndexOf(
                ActiveToken,
                StringComparison.Ordinal);
            token = ActiveToken;
        }
        if (tokenIndex < 0)
            return null;

        return guardActionName.Substring(0, tokenIndex) +
            LightParryToken +
            guardActionName.Substring(tokenIndex + token.Length);
    }
}
