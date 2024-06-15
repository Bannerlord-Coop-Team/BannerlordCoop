using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _isDisorganized
/// </summary>
public record IsDisorganizedChanged(bool IsDisorganized, string MobilePartyId) : IEvent
{
    public bool IsDisorganized { get; } = IsDisorganized;
    public string MobilePartyId { get; } = MobilePartyId;
}