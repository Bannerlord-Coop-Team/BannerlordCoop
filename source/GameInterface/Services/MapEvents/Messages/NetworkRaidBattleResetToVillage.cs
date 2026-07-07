using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Server -> all clients: a raid defender battle ended without looting the village. Clients
/// controlling one of <see cref="PartyIds"/> reset their local encounter back to the village menu.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRaidBattleResetToVillage : ICommand
{
    [ProtoMember(1)]
    public readonly string[] PartyIds;

    [ProtoMember(2)]
    public readonly string SettlementId;

    public NetworkRaidBattleResetToVillage(string[] partyIds, string settlementId)
    {
        PartyIds = partyIds ?? Array.Empty<string>();
        SettlementId = settlementId;
    }
}
