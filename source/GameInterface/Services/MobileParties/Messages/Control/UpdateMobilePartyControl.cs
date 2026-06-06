using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Control;

/// <summary>
/// Updates whether a mobile party is controlled locally. 
/// </summary>
public readonly struct UpdateMobilePartyControl : ICommand
{
    public readonly string ControllerId;
    public readonly string PartyId;

    /// <summary>
    /// Indicates whether control is being revoked (true) or granted (false).
    /// </summary>
    public readonly bool IsRevocation;

    /// <param name="isRevocation">Indicates whether control is being revoked (true) or granted (false).</param>
    public UpdateMobilePartyControl(string controllerId, string partyId, bool isRevocation = false)
    {
        ControllerId = controllerId;
        PartyId = partyId;
        IsRevocation = isRevocation;
    }
}
