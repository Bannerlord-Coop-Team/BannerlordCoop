using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Message from the server commanding a settlement encounter to start
/// For only the player party
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkStartSettlementEncounter : ICommand
{
    [ProtoMember(1)]
    public string SettlementId;
    [ProtoMember(2)]
    public string PartyId;

    public NetworkStartSettlementEncounter(NetworkRequestStartSettlementEncounter payload)
    {
        SettlementId = payload.SettlementId;
        PartyId = payload.PartyId;
    }
}