using System.Collections.Generic;

namespace Missions.Services.Network
{
    internal class MissionShard
    {
        public string InstanceName { get; private set; }
        public int ConnectedClients { get { return connectedClients.Count; } }
        List<string> connectedClients = new List<string>();
    }
}
