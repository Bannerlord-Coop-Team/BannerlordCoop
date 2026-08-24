using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Conversations;

/// <summary>Ends a local conversation before its mission agent is removed.</summary>
public interface ILocationConversationAgentGuard
{
    void EndConversationWithAgent(Agent agent);
}

/// <inheritdoc cref="ILocationConversationAgentGuard"/>
internal class LocationConversationAgentGuard : ILocationConversationAgentGuard
{
    public void EndConversationWithAgent(Agent agent)
    {
        var conversationManager = Campaign.Current?.ConversationManager;
        if (conversationManager?.IsConversationInProgress != true ||
            !ContainsAgent(conversationManager.ConversationAgents, agent))
            return;

        conversationManager.EndConversation();
    }

    internal static bool ContainsAgent(IEnumerable<IAgent> conversationAgents, Agent agent)
    {
        if (conversationAgents == null || agent == null) return false;

        foreach (var conversationAgent in conversationAgents)
        {
            if (ReferenceEquals(conversationAgent, agent))
                return true;
        }

        return false;
    }
}
