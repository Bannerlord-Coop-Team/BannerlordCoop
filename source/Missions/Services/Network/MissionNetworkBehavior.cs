using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Locations;
using Missions.Services.Agents.Messages;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network;

public class CoopMissionNetworkBehavior : MissionBehavior, ILocationMissionBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopMissionNetworkBehavior>();

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    private readonly IMessageBroker messageBroker;

    public CoopMissionNetworkBehavior(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        messageBroker.Publish(this, new AgentDeleted(affectedAgent));

        base.OnAgentDeleted(affectedAgent);
    }
}
