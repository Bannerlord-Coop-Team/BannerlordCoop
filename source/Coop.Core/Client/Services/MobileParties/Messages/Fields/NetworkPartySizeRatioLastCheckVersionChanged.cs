using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

[ProtoContract(SkipConstructor = true)]
public record NetworkPartySizeRatioLastCheckVersionChanged(int PartySizeRatioLastCheckVersion, string MobilePartyId) : ICommand
{
    public int PartySizeRatioLastCheckVersion { get; } = PartySizeRatioLastCheckVersion;
    public string MobilePartyId { get; } = MobilePartyId;
}