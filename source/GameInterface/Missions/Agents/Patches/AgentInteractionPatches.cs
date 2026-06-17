using Common.Messaging;
using HarmonyLib;
using SandBox.Conversation.MissionLogics;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.MountAndBlade;
using GameInterface.Missions.Agents.Extensions;
using GameInterface.Missions.Agents.Messages;

namespace GameInterface.Missions.Agents.Patches
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


            //Temporary

            AgentInteraction message = new AgentInteraction(userAgent, agent);
            MessageBroker.Instance.Publish(userAgent, message);
            return false;

            //
            
            return true;

        }
    }

    [HarmonyPatch(typeof(ConversationManager), "ProcessSentence")]
    public class ProcessSentencePatch
    {
        private static Agent requesterAgent;
        private static Agent targetAgent;
        public static bool Prefix(ConversationSentenceOption conversationSentenceOption)
        {
            if (targetAgent.IsNetworkAgent())
            {
                //MissionConversationLogic.Current.ConversationManager.EndConversation();
                //AgentInteraction message = new AgentInteraction(requesterAgent, targetAgent);
                //MessageBroker.Instance.Publish(requesterAgent, message);
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
