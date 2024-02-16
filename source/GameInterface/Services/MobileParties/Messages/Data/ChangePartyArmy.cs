using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Data;
public record ChangePartyArmy : ICommand
{
    public PartyArmyChangeData Data { get; }

    public ChangePartyArmy(PartyArmyChangeData data)
    {
        Data = data;
    }
}
