using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Command to remove an attached party
/// </summary>
public readonly struct RemoveAttachedParty : ICommand
{
    public readonly string MobilePartyId;
    public readonly string AttachedPartyId;

    public RemoveAttachedParty(string mobilePartiesId, string attachedPartyId)
    {
        MobilePartyId = mobilePartiesId;
        AttachedPartyId = attachedPartyId;
    }
}
