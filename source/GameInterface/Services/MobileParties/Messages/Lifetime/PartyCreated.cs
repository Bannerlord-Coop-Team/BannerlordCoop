using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;
public class PartyCreated : IEvent
{
    public PartyCreationData Data { get; }

    public PartyCreated(PartyCreationData data)
    {
        Data = data;
    }
}
