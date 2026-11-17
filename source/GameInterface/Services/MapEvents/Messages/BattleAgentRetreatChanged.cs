using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event raised on the game thread when an agent's <see cref="Agent.IsRunningAway"/> changes in a coop
/// battle, so the Missions battle controller can mirror the change to peers when this node owns the agent.
/// </summary>
public record BattleAgentRetreatChanged : IEvent
{
    public Agent Agent { get; }
    public bool IsRunningAway { get; }

    public BattleAgentRetreatChanged(Agent agent, bool isRunningAway)
    {
        Agent = agent;
        IsRunningAway = isRunningAway;
    }
}
