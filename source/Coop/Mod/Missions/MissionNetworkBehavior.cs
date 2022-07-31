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

        private readonly LiteNetP2PClient m_Client;
        private MissionClient missionClient;

        private readonly TimeSpan WaitForConnectionsTime = TimeSpan.FromSeconds(1);

        public MissionNetworkBehavior()
        {
            m_Client = new LiteNetP2PClient(new NetworkConfiguration());

            Main.Instance.Updateables.Add(m_Client);
        }

        public override void OnAfterMissionCreated()
        {
            
            base.OnAfterMissionCreated();

            // TODO find callback for loading mission
            Task.Factory.StartNew(async () =>
            {
                while (Mission == null || Mission.IsLoadingFinished == false) { await Task.Delay(100); }
                m_Client.ConnectToP2PServer(Mission.SceneName);

                // TODO remove test code
                MissionTestGameManager.StartArenaFight();

                missionClient = new MissionClient(m_Client);
                await Task.Delay(WaitForConnectionsTime);
                missionClient.SendJoinRequest();
            });
        }
    }
}
