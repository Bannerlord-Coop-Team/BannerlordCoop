using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Command to remove an attached party
/// </summary>
public record RemoveAttachedParty : ICommand
{
    public AttachedPartyData AttachedPartyData { get; }

    public RemoveAttachedParty(AttachedPartyData attachedPartyData)
    {
        AttachedPartyData = attachedPartyData;
    }
}
