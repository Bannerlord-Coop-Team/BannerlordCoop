using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.CoopBattle
{
    public class BattleNetwork
    {

        public IMessageBroker messageBroker;

        public BattlePlayer player;

        public List<NetworkAgent> networkAgents;

        public List<INetworkInterface> connectedClients;

    }
}
