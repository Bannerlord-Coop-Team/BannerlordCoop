using Common.Messaging;
using Coop.Mod.Missions;
using Missions.Config;
using Missions.Network;
using System;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Missions
{
    public class MissionNetworkBehavior : MissionBehavior
    {
        public IMessageBroker MessageBroker { get; private set; }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private LiteNetP2PClient m_Client;
        private MissionClient missionClient;

        private readonly TimeSpan WaitForConnectionsTime = TimeSpan.FromSeconds(1);

        public MissionNetworkBehavior()
        {
            m_Client = new LiteNetP2PClient(new NetworkConfiguration());

            // TODO add to updateables

            // TODO find callback for loading mission
            Task.Factory.StartNew(async () =>
            {
                while (Mission == null || Mission.IsLoadingFinished == false)
                {
                    await Task.Delay(100);
                }
                m_Client.ConnectToP2PServer(Mission.SceneName);

                missionClient = new MissionClient(m_Client);
                await Task.Delay(WaitForConnectionsTime);
                missionClient.SendJoinRequest();
            });
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();

            NetworkAgentRegistry.Clear();

            // TODO remove from updateables
            m_Client = null;
            missionClient = null;
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            if (NetworkAgentRegistry.AgentToId.TryGetValue(affectedAgent, out Guid agentId))
            {
                NetworkAgentRegistry.RemoveNetworkControlledAgent(agentId);
            }

            base.OnAgentDeleted(affectedAgent);
        }
    }
}
