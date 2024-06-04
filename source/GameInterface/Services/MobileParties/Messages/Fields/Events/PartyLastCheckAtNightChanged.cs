using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

public record PartyLastCheckAtNightChanged(bool PartyLastCheckAtNight, string MobilePartyId) : IEvent
{
    public bool PartyLastCheckAtNight { get; } = PartyLastCheckAtNight;
    public string MobilePartyId { get; } = MobilePartyId;
}