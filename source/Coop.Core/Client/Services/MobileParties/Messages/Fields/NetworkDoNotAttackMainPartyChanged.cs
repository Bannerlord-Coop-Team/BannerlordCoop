using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _doNotAttackMainParty
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkDoNotAttackMainPartyChanged(int DoNotAttackMainParty, string MobilePartyId) : ICommand
{
    public int DoNotAttackMainParty { get; } = DoNotAttackMainParty;
    public string MobilePartyId { get; } = MobilePartyId;
}