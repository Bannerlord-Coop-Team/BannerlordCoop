using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CoopTestMod
{
    public class ClientAgentManager
    {
        private static ClientAgentManager instance;
        private ConcurrentDictionary<string, int> agentIdToIndexMap = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<int, string> agentIndexToNetworkIdMap = new ConcurrentDictionary<int, string>();
        private ConcurrentDictionary<int, NetworkAgent> indexToNetworkAgent = new ConcurrentDictionary<int, NetworkAgent>();
        public static ClientAgentManager Instance()
        {
            if(instance == null)
            {
                instance = new ClientAgentManager();
            }
            return instance;
        }

        public List<NetworkAgent> getHostNetworkAgents()
        {
            return indexToNetworkAgent.Values.Where(n => n.IsHostSyncAgent).ToList();
        }

        public bool IsNetworkAgent(int index)
        {
            return agentIndexToNetworkIdMap.ContainsKey(index);
        }

        public void addNetworkAgent(NetworkAgent agent)
        {
            agentIdToIndexMap[agent.AgentID] = agent.AgentIndex;
            agentIndexToNetworkIdMap[agent.AgentIndex] = agent.AgentID;
            indexToNetworkAgent[agent.AgentIndex] = agent;
        }

        public bool containsAgent(string id)
        {
            return agentIdToIndexMap.ContainsKey(id);
        }

        public int getIndexFromId(string agentId)
        {
            if (agentIdToIndexMap.ContainsKey(agentId))
            {
                return agentIdToIndexMap[agentId];
            }
            return -1;
        }

        public string getIdFromIndex(int index)
        {
            if (agentIndexToNetworkIdMap.ContainsKey(index))
            {
                return agentIndexToNetworkIdMap[index];
            }
            return null;
        }

        public bool isHostSyncAgent(int index)
        {
            if (!indexToNetworkAgent.ContainsKey(index))
            {
                return false;
            }
            return indexToNetworkAgent[index].IsHostSyncAgent;
        }
    }
}
