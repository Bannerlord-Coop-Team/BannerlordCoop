using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages.Lifetime;

/// <summary>
/// Event that is published when a party is created on the server.
/// </summary>
public record ArmyCreated : IEvent
{
    public ArmyCreationData Data { get; }

    public ArmyCreated(ArmyCreationData data)
    {
        Data = data;
    }
}
