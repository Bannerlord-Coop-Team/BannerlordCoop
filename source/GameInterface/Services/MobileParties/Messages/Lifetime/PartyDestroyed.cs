using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Event that is published when a party is destroyed on the server.
/// </summary>
public record PartyDestroyed : IEvent
{
    public PartyDestructionData Data { get; }

    public PartyDestroyed(PartyDestructionData data)
    {
        Data = data;
    }
}
