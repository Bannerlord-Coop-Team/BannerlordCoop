using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages.Lifetime;

/// <summary>
/// Event that is published when a party is destroyed on the server.
/// </summary>
public record ArmyDestroyed : IEvent
{
    public ArmyDestructionData Data { get; }

    public ArmyDestroyed(ArmyDestructionData data)
    {
        Data = data;
    }
}
