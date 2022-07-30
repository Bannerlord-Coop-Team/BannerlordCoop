using Common.Messaging;
using Coop.Mod.Mission.Network;
using Coop.NetImpl.LiteNet;
using Network.Infrastructure;
using RailgunNet.Connection.Server;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Mission
{
    public class MissionNetworkBehavior : MissionBehavior
    {
        public IMessageBroker MessageBroker { get; private set; }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;
        private readonly LiteNetP2PClient m_Client;
        public MissionNetworkBehavior()
        {
            m_Client = new LiteNetP2PClient(new NetworkConfiguration());
            m_Client.ConnectToP2PServer(Mission.SceneName);
            MessageBroker = new NetworkMessageBroker(m_Client);
        }


    }
}
