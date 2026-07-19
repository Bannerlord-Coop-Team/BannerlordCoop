using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Message from the server rejecting a requested settlement encounter.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkSettlementEncounterRejected : ICommand
{
    [ProtoMember(1)]
    public string PartyId;

    [ProtoMember(2)]
    public string SettlementId;

    public NetworkSettlementEncounterRejected(NetworkRequestStartSettlementEncounter payload)
    {
        PartyId = payload.PartyId;
        SettlementId = payload.SettlementId;
    }
}
