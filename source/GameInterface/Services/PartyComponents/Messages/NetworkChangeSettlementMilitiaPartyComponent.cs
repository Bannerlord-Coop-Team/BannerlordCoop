using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;
[ProtoContract(SkipConstructor = true)]
internal class NetworkChangeSettlementMilitiaPartyComponent : ICommand
{
    public NetworkChangeSettlementMilitiaPartyComponent(string componentId, string settlementId)
    {
        ComponentId = componentId;
        SettlementId = settlementId;
    }

    [ProtoMember(1)]
    public string ComponentId { get; }

    [ProtoMember(2)]
    public string SettlementId { get; }
}
