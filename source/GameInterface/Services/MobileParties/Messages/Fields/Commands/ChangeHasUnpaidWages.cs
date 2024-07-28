using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for HasUnpaidWages
/// </summary>
/// <param name="HasUnpaidWages"></param>
/// <param name="MobilePartyId"></param>
public record ChangeHasUnpaidWages(float HasUnpaidWages, string MobilePartyId) : ICommand
{
    public float HasUnpaidWages { get; } = HasUnpaidWages;
    public string MobilePartyId { get; } = MobilePartyId;
}