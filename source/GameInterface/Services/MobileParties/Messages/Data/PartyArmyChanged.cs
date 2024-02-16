using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Data;
public class PartyArmyChanged : IEvent
{
    public PartyArmyChangeData Data { get; }

    public PartyArmyChanged(PartyArmyChangeData data)
    {
        Data = data;
    }
}
