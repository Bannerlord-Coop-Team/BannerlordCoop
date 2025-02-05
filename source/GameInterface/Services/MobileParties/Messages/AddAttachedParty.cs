using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Command to add an attached party
/// </summary>
public record AddAttachedParty : ICommand
{
    public AttachedPartyData AttachedPartyData { get; }

    public AddAttachedParty(AttachedPartyData attachedPartyData)
    {
        AttachedPartyData = attachedPartyData;
    }
}
