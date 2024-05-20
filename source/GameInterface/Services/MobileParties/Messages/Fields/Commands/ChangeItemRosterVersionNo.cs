using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

public record ChangeItemRosterVersionNo(int ItemRosterVersionNo, string MobilePartyId) : IEvent
{
    public int ItemRosterVersionNo { get; } = ItemRosterVersionNo;
    public string MobilePartyId { get; } = MobilePartyId;
}