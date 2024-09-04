using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

public record PartySizeRatioLastCheckVersionChanged(int PartySizeRatioLastCheckVersion, string MobilePartyId) : IEvent
{
    public int PartySizeRatioLastCheckVersion { get; } = PartySizeRatioLastCheckVersion;
    public string MobilePartyId { get; } = MobilePartyId;
}