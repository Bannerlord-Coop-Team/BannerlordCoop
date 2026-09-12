using GameInterface.Services.Locations.Conversations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Locations;

/// <summary>Checks conversation-agent identity matching used before local and remote despawns.</summary>
public class LocationConversationAgentGuardTests
{
    [Fact]
    public void ConversationAgentMatch_UsesAgentIdentity()
    {
        var target = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var other = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var absent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        IAgent[] conversationAgents = { other, target };

        Assert.True(LocationConversationAgentGuard.ContainsAgent(conversationAgents, target));
        Assert.False(LocationConversationAgentGuard.ContainsAgent(conversationAgents, absent));
        Assert.False(LocationConversationAgentGuard.ContainsAgent(conversationAgents, null));
    }
}
