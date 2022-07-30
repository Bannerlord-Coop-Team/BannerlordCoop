using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Mission
{
    internal class MissionShard
    {
        public string InstanceName { get; private set; }
        public int ConnectedClients { get { return connectedClients.Count; } }
        List<string> connectedClients = new List<string>();
    }
}
