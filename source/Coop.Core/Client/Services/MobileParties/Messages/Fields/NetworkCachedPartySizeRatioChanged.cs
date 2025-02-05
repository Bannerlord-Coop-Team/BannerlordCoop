using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _cachedPartySizeRatio
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCachedPartySizeRatioChanged(float CachedPartySizeRatio, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public float CachedPartySizeRatio { get; } = CachedPartySizeRatio;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}