using HarmonyLib;
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
            if (!agent.Character.IsPlayerCharacter)
            {
                return true;
            }

            OnAgentInteraction?.Invoke(userAgent, agent);

            return false;

        }
    }
}
