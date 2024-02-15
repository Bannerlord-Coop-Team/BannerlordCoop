using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Event that is published when a party is created on the server.
/// </summary>
public record PartyCreated : IEvent
{
    public PartyCreationData Data { get; }

    public PartyCreated(PartyCreationData data)
    {
        Data = data;
    }
}
