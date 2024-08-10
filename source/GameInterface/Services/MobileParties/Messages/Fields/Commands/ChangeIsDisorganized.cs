using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _isDisorganized
/// </summary>
public record ChangeIsDisorganized(bool IsDisorganized, string MobilePartyId) : ICommand
{
    public bool IsDisorganized { get; } = IsDisorganized;
    public string MobilePartyId { get; } = MobilePartyId;
}