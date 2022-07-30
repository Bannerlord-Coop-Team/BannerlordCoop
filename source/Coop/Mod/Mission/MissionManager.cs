using Common.Messaging;
using Network.Infrastructure;
using RailgunNet.Connection.Server;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Mission
{
    public class MissionManager : MissionBehavior
    {
        Dictionary<string, MissionShard> ActiveShards = new Dictionary<string, MissionShard>();

        public MissionManager(IMessageBroker messageBroker)
        {
            MessageBroker = messageBroker;
        }

        public IMessageBroker MessageBroker { get; private set; }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

        public override void OnCreated()
        {
            string scene = Mission.SceneName;
            base.OnCreated();
        }

        public bool AddClient(RailServerPeer peer, string instance)
        {
            // TODO
            if (ActiveShards.ContainsKey(instance))
            {
                ActiveShards.Add(instance, new MissionShard());
            }
            else
            {
                ActiveShards.Add(instance, new MissionShard());
            }
            return true;
        }

        public void RemoveClient(RailServerPeer peer)
        {

        }
    }
}
