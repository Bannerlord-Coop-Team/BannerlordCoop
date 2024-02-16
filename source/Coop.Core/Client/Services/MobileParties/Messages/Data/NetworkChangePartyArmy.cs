using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Data;

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