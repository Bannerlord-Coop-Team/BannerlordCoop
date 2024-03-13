using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Event when a party is removed from the attached party list
/// </summary>
public record AttachedPartyRemoved : IEvent
{
    public AttachedPartyData AttachedPartyData { get; }

    public AttachedPartyRemoved(AttachedPartyData attachedPartyData)
    {
        AttachedPartyData = attachedPartyData;
    }
}
