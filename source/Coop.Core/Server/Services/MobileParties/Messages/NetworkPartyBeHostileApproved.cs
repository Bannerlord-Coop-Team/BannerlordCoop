using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Party become hostile approved by server
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPartyBeHostileApproved : ICommand
{
    [ProtoMember(1)]
    public string AttackerPartyId { get; }
    [ProtoMember(2)]
    public string DefenderPartyId { get; }
    [ProtoMember(3)]
    public float Value { get; }

    public NetworkPartyBeHostileApproved(string attackerPartyId, string defenderPartyId, float value)
    {
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
        Value = value;
    }
}
