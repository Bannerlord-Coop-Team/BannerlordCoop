using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

[ProtoContract(SkipConstructor = true)]
public record NetworkItemRosterVersionNoChanged(int ItemRosterVersionNo, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public int ItemRosterVersionNo { get; } = ItemRosterVersionNo;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}