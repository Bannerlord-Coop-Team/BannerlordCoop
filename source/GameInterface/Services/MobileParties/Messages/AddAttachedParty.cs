using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Command to add an attached party
/// </summary>
public readonly struct AddAttachedParty : ICommand
{
    public readonly string MobilePartyId;
    public readonly string AttachedPartyId;

    public AddAttachedParty(string mobilePartiesId, string attachedPartyId)
    {
        MobilePartyId = mobilePartiesId;
        AttachedPartyId = attachedPartyId;
    }
}
