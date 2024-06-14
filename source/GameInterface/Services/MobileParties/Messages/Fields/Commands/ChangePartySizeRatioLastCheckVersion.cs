using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

public record ChangePartySizeRatioLastCheckVersion(int PartySizeRatioLastCheckVersion, string MobilePartyId) : IEvent
{
    public int PartySizeRatioLastCheckVersion { get; } = PartySizeRatioLastCheckVersion;
    public string MobilePartyId { get; } = MobilePartyId;
}