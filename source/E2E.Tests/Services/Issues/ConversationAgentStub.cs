using TaleWorlds.Core;

namespace E2E.Tests.Services.Issues;

internal sealed class ConversationAgentStub : IAgent
{
    public ConversationAgentStub(BasicCharacterObject character)
    {
        Character = character;
    }

    public BasicCharacterObject Character { get; }

    public AgentState State => AgentState.Active;

    public IMissionTeam Team => null!;

    public IAgentOriginBase Origin => null!;

    public float Age => 30f;

    public bool IsEnemyOf(IAgent agent) => false;

    public bool IsFriendOf(IAgent agent) => true;

    public bool IsActive() => true;

    public void SetAsConversationAgent(bool set)
    {
    }

    public void OnConversationStarted()
    {
    }
}
