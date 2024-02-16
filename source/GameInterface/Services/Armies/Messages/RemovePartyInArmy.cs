using Common.Messaging;
using GameInterface.Services.Armies.Data;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to remove a MobileParty from an Army
/// </summary>
public record RemovePartyInArmy : ICommand
{
    public ArmyRemovePartyData Data { get; }

    public RemovePartyInArmy(ArmyRemovePartyData data)
    {
        Data = data;
    }
}
