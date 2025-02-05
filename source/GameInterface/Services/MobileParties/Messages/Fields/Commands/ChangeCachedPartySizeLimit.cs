using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _cachedPartySizeLimit
/// </summary>
public record ChangeCachedPartySizeLimit(int CachedPartySizeLimit, string MobilePartyId) : ICommand
{
    public int CachedPartySizeLimit { get; } = CachedPartySizeLimit;
    public string MobilePartyId { get; } = MobilePartyId;
}