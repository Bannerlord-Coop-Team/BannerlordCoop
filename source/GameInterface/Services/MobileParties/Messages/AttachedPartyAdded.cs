using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Event when a party is added from the attached party list
/// </summary>
public record AttachedPartyAdded : IEvent
{
    public AttachedPartyData AttachedPartyData { get; }

    public AttachedPartyAdded(AttachedPartyData attachedPartyData)
    {
        AttachedPartyData = attachedPartyData;
    }
}
