using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _attachedTo
/// </summary>
public record ChangeAttachedTo(string AttachedToId, string MobilePartyId) : ICommand
{
    /// <summary>
    /// ID of a mobile party.
    /// </summary>
    public string AttachedToId { get; } = AttachedToId;

    public string MobilePartyId { get; } = MobilePartyId;
}