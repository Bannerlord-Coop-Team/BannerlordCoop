using Common.Messaging;
using Missions.Services.Agents.Messages;
using Missions.Services.Messaging;
using System;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network
{
    public class MissionNetworkBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private LiteNetP2PClient m_Client;
        private MissionClient missionClient;

        private readonly TimeSpan WaitForConnectionsTime = TimeSpan.FromSeconds(1);

        private readonly INetworkMessageBroker _messageBroker;

        internal MissionNetworkBehavior(LiteNetP2PClient client, INetworkMessageBroker messageBroker)
        {
            m_Client = client;
            _messageBroker = messageBroker;

            // TODO find callback for loading mission
            Task.Factory.StartNew(async () =>
            {
                while (Mission == null || Mission.IsLoadingFinished == false)
                {
                    await Task.Delay(100);
                }

                string sceneName = Mission.SceneName;
                m_Client.NatPunch(sceneName);

                missionClient = new MissionClient(m_Client, _messageBroker);
                await Task.Delay(WaitForConnectionsTime);
            });
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();

            missionClient.Dispose();
            m_Client.Stop();
            m_Client = null;
            missionClient = null;
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            _messageBroker.Publish(this, new AgentDeleted(affectedAgent));
            

            base.OnAgentDeleted(affectedAgent);
        }

        protected override void OnEndMission()
        {
            m_Client.Dispose();
            MBGameManager.EndGame();
            base.OnEndMission();
        }
    }
}
