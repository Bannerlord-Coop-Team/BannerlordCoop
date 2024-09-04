using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _doNotAttackMainParty
/// </summary>
public record ChangeDoNotAttackMainParty(int DoNotAttackMainParty, string MobilePartyId) : ICommand
{
    public int DoNotAttackMainParty { get; } = DoNotAttackMainParty;
    public string MobilePartyId { get; } = MobilePartyId;
}