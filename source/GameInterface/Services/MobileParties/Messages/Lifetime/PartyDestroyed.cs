using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;
public class PartyDestroyed : IEvent
{
    public PartyDestructionData Data { get; }

    public PartyDestroyed(PartyDestructionData data)
    {
        Data = data;
    }
}
