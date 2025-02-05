using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _cachedPartySizeLimit
/// </summary>
public record CachedPartySizeLimitChanged(int CachedPartySizeLimit, string MobilePartyId) : IEvent
{
    public int CachedPartySizeLimit { get; } = CachedPartySizeLimit;
    public string MobilePartyId { get; } = MobilePartyId;
}