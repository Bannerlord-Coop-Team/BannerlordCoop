using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Event from GameInterface for HasUnpaidWages
/// </summary>
/// <param name="HasUnpaidWages"></param>
/// <param name="MobilePartyId"></param>
public record HasUnpaidWagesChanged(float HasUnpaidWages, string MobilePartyId) : IEvent
{
    public float HasUnpaidWages { get; } = HasUnpaidWages;
    public string MobilePartyId { get; } = MobilePartyId;
}