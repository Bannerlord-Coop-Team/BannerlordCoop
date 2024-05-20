using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

[ProtoContract(SkipConstructor = true)]
public record NetworkItemRosterVersionNoChanged(int ItemRosterVersionNo, string MobilePartyId) : ICommand
{
    public int ItemRosterVersionNo { get; } = ItemRosterVersionNo;
    public string MobilePartyId { get; } = MobilePartyId;
}