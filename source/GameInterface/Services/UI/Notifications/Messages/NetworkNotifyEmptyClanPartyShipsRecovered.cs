using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyEmptyClanPartyShipsRecovered : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyLeaderId;

    [ProtoMember(2)]
    public readonly int Gold;

    public NetworkNotifyEmptyClanPartyShipsRecovered(string partyLeaderId, int gold)
    {
        PartyLeaderId = partyLeaderId;
        Gold = gold;
    }
}
