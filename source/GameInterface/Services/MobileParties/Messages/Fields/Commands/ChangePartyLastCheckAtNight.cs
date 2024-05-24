using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

public record ChangePartyLastCheckAtNight(bool PartyLastCheckAtNight, string MobilePartyId) : IEvent
{
    public bool PartyLastCheckAtNight { get; } = PartyLastCheckAtNight;
    public string MobilePartyId { get; } = MobilePartyId;
}