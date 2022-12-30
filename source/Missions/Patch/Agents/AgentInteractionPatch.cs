using HarmonyLib;
using Missions.Extensions;
using SandBox.Conversation.MissionLogics;
using System;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Patch.Agents
{
    [HarmonyPatch(typeof(MissionConversationLogic), "OnAgentInteraction")]
    public class AgentInteractionPatch
    {
        public static event Action<Agent, Agent> OnAgentInteraction;
        static bool Prefix(ref Agent userAgent, ref Agent agent)
        {
            if (!agent.IsNetworkAgent())
            {
                return true;
            }

            // TODO fix board games
            OnAgentInteraction?.Invoke(userAgent, agent);

            return false;

        }
    }
}
