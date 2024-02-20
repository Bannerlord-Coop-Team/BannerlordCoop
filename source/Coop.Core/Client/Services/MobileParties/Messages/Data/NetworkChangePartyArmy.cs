using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Data;

/// <summary>
/// Command to change the army of a party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkChangePartyArmy : ICommand
{
    [ProtoMember(1)]
    public PartyArmyChangeData Data { get; }

    public NetworkChangePartyArmy(PartyArmyChangeData data)
    {
        Data = data;
    }
}