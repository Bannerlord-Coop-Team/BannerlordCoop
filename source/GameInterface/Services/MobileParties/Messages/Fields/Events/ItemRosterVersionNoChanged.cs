using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

public record ItemRosterVersionNoChanged(int ItemRosterVersionNo, string MobilePartyId) : IEvent
{
    public int ItemRosterVersionNo { get; } = ItemRosterVersionNo;
    public string MobilePartyId { get; } = MobilePartyId;
}