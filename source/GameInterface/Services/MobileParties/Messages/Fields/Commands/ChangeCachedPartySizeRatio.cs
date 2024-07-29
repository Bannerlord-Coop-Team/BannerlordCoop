using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _cachedPartySizeRatio
/// </summary>
public record ChangeCachedPartySizeRatio(float CachedPartySizeRatio, string MobilePartyId) : ICommand
{
    public float CachedPartySizeRatio { get; } = CachedPartySizeRatio;
    public string MobilePartyId { get; } = MobilePartyId;
}