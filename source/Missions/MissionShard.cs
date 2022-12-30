using System.Collections.Generic;

namespace Missions
{
    internal class MissionShard
    {
        public string InstanceName { get; private set; }
        public int ConnectedClients { get { return connectedClients.Count; } }
        List<string> connectedClients = new List<string>();
    }
}
