using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.CoopBattle
{
    public class BattleNetwork
    {

        public MessageBroker messageBroker;

        public BattlePlayer player;

        public List<CoopAgent> networkAgents;

        public List<INetworkInterface> connectedClients;

    }
}
