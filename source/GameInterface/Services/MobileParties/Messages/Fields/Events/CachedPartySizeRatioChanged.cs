using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _cachedPartySizeRatio
/// </summary>
public record CachedPartySizeRatioChanged(float CachedPartySizeRatio, string MobilePartyId) : IEvent
{
    public float CachedPartySizeRatio { get; } = CachedPartySizeRatio;
    public string MobilePartyId { get; } = MobilePartyId;
}