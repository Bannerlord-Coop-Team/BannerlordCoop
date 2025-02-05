using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _isCurrentlyUsedByAQuest
/// </summary>
public record IsCurrentlyUsedByAQuestChanged(bool IsCurrentlyUsedByAQuest, string MobilePartyId) : IEvent
{
    public bool IsCurrentlyUsedByAQuest { get; } = IsCurrentlyUsedByAQuest;
    public string MobilePartyId { get; } = MobilePartyId;
}