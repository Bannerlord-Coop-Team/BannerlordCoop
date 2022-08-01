using Common.Messaging;
using Coop.Mod.Events;
using Coop.Mod.Missions.Network;
using Coop.NetImpl.LiteNet;
using Network.Infrastructure;
using RailgunNet.Connection.Server;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
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

            Main.Instance.Updateables.Add(m_Client);

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

            Main.Instance.Updateables.Remove(m_Client);
            m_Client = null;
            missionClient = null;
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            if(NetworkAgentRegistry.AgentToId.TryGetValue(affectedAgent, out Guid agentId))
            {
                NetworkAgentRegistry.RemoveNetworkControlledAgent(agentId);
            }
            
            base.OnAgentDeleted(affectedAgent);
        }
    }
}
