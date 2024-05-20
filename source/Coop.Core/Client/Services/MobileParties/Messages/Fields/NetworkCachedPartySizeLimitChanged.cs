using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _cachedPartySizeLimit
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCachedPartySizeLimitChanged(int CachedPartySizeLimit, string MobilePartyId) : ICommand
{
    public int CachedPartySizeLimit { get; } = CachedPartySizeLimit;
    public string MobilePartyId { get; } = MobilePartyId;
}