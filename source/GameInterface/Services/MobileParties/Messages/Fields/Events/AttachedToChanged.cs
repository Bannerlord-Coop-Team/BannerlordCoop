using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Event from GameInterface for _attachedTo
/// </summary>
public record AttachedToChanged(string AttachedToId, string MobilePartyId) : IEvent
{
    /// <summary>
    /// ID of a mobile party.
    /// </summary>
    public string AttachedToId { get; } = AttachedToId;

    public string MobilePartyId { get; } = MobilePartyId;
}