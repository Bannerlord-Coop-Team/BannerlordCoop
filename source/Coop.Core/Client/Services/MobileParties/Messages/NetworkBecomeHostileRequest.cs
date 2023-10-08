using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Party become hostile request
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkBecomeHostileRequest : ICommand
{
    [ProtoMember(1)]
    public string AttackerPartyId { get; }
    [ProtoMember(2)]
    public string DefenderPartyId { get; }
    [ProtoMember(3)]
    public float Value { get; }

    public NetworkBecomeHostileRequest(string attackerPartyId, string defenderPartyId, float value)
    {
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
        Value = value;
    }
}