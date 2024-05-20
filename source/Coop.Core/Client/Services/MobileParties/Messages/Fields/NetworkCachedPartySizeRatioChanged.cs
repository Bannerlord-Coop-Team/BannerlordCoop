using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _cachedPartySizeRatio
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCachedPartySizeRatioChanged(float CachedPartySizeRatio, string MobilePartyId) : ICommand
{
    public float CachedPartySizeRatio { get; } = CachedPartySizeRatio;
    public string MobilePartyId { get; } = MobilePartyId;
}