using Common.Messaging;
using Network.Infrastructure;
using RailgunNet.Connection.Server;
using System.Collections.Generic;

namespace Coop.Mod.Mission
{
    public class MissionManager
    {
        Dictionary<string, MissionShard> ActiveShards = new Dictionary<string, MissionShard>();

        public MissionManager(IMessageBroker messageBroker)
        {
            MessageBroker = messageBroker;
        }

        public IMessageBroker MessageBroker { get; private set; }

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
