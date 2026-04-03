using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Message from the client requesting that a party encounter be started (e.g., talking to a lord on the map)
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkRequestStartPartyEncounter : ICommand
{
    [ProtoMember(1)]
    public string AttackerPartyId { get; }
    [ProtoMember(2)]
    public string DefenderPartyId { get; }

    public NetworkRequestStartPartyEncounter(string attackerPartyId, string defenderPartyId)
    {
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
    }
}
