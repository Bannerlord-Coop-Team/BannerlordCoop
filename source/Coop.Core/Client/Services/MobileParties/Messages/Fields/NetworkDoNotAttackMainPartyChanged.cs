using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _doNotAttackMainParty
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkDoNotAttackMainPartyChanged(int DoNotAttackMainParty, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public int DoNotAttackMainParty { get; } = DoNotAttackMainParty;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}