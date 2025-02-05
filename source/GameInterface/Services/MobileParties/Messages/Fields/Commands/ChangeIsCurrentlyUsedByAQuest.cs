using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _isCurrentlyUsedByAQuest
/// </summary>
public record ChangeIsCurrentlyUsedByAQuest(bool IsCurrentlyUsedByAQuest, string MobilePartyId) : ICommand
{
    public bool IsCurrentlyUsedByAQuest { get; } = IsCurrentlyUsedByAQuest;
    public string MobilePartyId { get; } = MobilePartyId;
}