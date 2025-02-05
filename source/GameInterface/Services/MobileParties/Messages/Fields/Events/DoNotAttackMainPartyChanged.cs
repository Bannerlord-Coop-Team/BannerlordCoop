using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _doNotAttackMainParty
/// </summary>
public record DoNotAttackMainPartyChanged(int DoNotAttackMainParty, string MobilePartyId) : IEvent
{
    public int DoNotAttackMainParty { get; } = DoNotAttackMainParty;
    public string MobilePartyId { get; } = MobilePartyId;
}