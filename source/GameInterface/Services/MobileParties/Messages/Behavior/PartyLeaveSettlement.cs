using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Commands a party to leave a settlement
/// </summary>
[DontLogMessage]
public record PartyLeaveSettlement : ICommand
{
    public string PartyId { get; }

    public PartyLeaveSettlement(string partyId)
    {
        PartyId = partyId;
    }
}