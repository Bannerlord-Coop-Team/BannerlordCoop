using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CoopTestMod
{
    // This class allows the client to manage agents. Mainly converting them back and forth between network IDs and indexes 
    public class ClientAgentManager
    {
        private static ClientAgentManager instance;
        private ConcurrentDictionary<string, int> agentIdToIndexMap = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<int, string> agentIndexToNetworkIdMap = new ConcurrentDictionary<int, string>();
        private ConcurrentDictionary<int, NetworkAgent> indexToNetworkAgent = new ConcurrentDictionary<int, NetworkAgent>();
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static ClientAgentManager Instance()
        {
            if (instance == null)
            {
                instance = new ClientAgentManager();
            }
            return instance;
        }

        public List<NetworkAgent> GetHostNetworkAgents()
        {
            return indexToNetworkAgent.Values.Where(n => n.IsHostSyncAgent).ToList();
        }

        public bool IsNetworkAgent(int index)
        {
            return agentIndexToNetworkIdMap.ContainsKey(index);
        }

        public void AddNetworkAgent(NetworkAgent agent)
        {
            agentIdToIndexMap[agent.AgentID] = agent.AgentIndex;
            agentIndexToNetworkIdMap[agent.AgentIndex] = agent.AgentID;
            indexToNetworkAgent[agent.AgentIndex] = agent;
        }

        public bool ContainsAgent(string id)
        {
            return agentIdToIndexMap.ContainsKey(id);
        }

        public int GetIndexFromId(string agentId)
        {
            if (agentIdToIndexMap.ContainsKey(agentId))
            {
                return agentIdToIndexMap[agentId];
            }
            return -1;
        }

        public string GetIdFromIndex(int index)
        {
            if (agentIndexToNetworkIdMap.ContainsKey(index))
            {
                return agentIndexToNetworkIdMap[index];
            }
            return null;
        }

        public bool IsHostSyncAgent(int index)
        {
            if (!indexToNetworkAgent.ContainsKey(index))
            {
                return false;
            }
            return indexToNetworkAgent[index].IsHostSyncAgent;
        }

        public void RemoveAgent(string agentIndex)
        {

        }

        public void ClearAll()
        {
            agentIdToIndexMap.Clear();
            agentIndexToNetworkIdMap.Clear();
            indexToNetworkAgent.Clear();

        }

        public int AgentCount()
        {
            return indexToNetworkAgent.Count;
        }

        public NetworkAgent GetNetworkAgent(string id)
        {
            return indexToNetworkAgent[agentIdToIndexMap[id]];
        }
    }
}
