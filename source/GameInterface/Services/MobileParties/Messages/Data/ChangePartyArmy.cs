using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Data;

/// <summary>
/// Command to change the army of a party.
/// </summary>
public record ChangePartyArmy : ICommand
{
    public PartyArmyChangeData Data { get; }

    public ChangePartyArmy(PartyArmyChangeData data)
    {
        Data = data;
    }
}
