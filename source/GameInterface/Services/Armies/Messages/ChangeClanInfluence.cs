using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a clan influence is changed
/// </summary>
public readonly struct ChangeClanInfluence : IEvent
{
    public readonly int Influence;

    public ChangeClanInfluence(int influence)
    {
        Influence = influence;
    }
}
