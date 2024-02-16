using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add a MobileParty to an Army
/// </summary>
public record AddMobilePartyInArmy : ICommand
{
    public ArmyAddPartyData Data { get; }

    public AddMobilePartyInArmy(ArmyAddPartyData data)
    {
        Data = data;
    }
}
