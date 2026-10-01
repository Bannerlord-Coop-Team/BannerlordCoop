using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Message from the client requesting a settlement encounter to end
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestEndSettlementEncounter : ICommand
{
    [ProtoMember(1)]
    public uint PartyId { get; }

    public NetworkRequestEndSettlementEncounter(uint partyId)
    {
        PartyId = partyId;
    }
}
