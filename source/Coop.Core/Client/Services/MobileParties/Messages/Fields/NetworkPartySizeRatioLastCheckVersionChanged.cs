using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

[ProtoContract(SkipConstructor = true)]
public record NetworkPartySizeRatioLastCheckVersionChanged(int PartySizeRatioLastCheckVersion, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public int PartySizeRatioLastCheckVersion { get; } = PartySizeRatioLastCheckVersion;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}