using HarmonyLib;
using Missions.Extensions;
using SandBox.Conversation.MissionLogics;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Patch.Agents
{
    [HarmonyPatch(typeof(MissionConversationLogic), "OnAgentInteraction")]
    public class AgentInteractionPatch
    {
        public static bool Prefix(ref Agent userAgent, ref Agent agent)
        {
            if (!agent.IsNetworkAgent())
            {
                ProcessSentencePatch.SetInteractedAgents(null, null);
                return true;
            }
            
            ProcessSentencePatch.SetInteractedAgents(userAgent, agent);
            return true;

        }
    }

    [HarmonyPatch(typeof(ConversationManager), "ProcessSentence")]
    public class ProcessSentencePatch
    {
        private static Agent requesterAgent;
        private static Agent targetAgent;

        public static event Action<Agent, Agent> OnAgentInteraction;
        public static bool Prefix(ConversationSentenceOption conversationSentenceOption)
        {
            if (conversationSentenceOption.Id == "lord_player_start_game" && targetAgent.IsNetworkAgent())
            {
                MissionConversationLogic.Current.ConversationManager.EndConversation();
                OnAgentInteraction?.Invoke(requesterAgent, targetAgent);
                return false;
            }
            return true;
        }
        public static void SetInteractedAgents(Agent reqAgent, Agent tarAgent)
        {
            requesterAgent = reqAgent;
            targetAgent = tarAgent;
        }
    }
}
