using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkRequestCreateKingdom : ICommand
{
    [ProtoMember(1)]
    public string ControllerId { get; }
    [ProtoMember(2)]
    public string KingdomName { get; }
    [ProtoMember(3)]
    public string CultureId { get; }
    [ProtoMember(4)]
    public string PartyId { get; }
    [ProtoMember(5)]
    public string SettlementId { get; }

    public NetworkRequestCreateKingdom(string controllerId, string kingdomName, string cultureId)
        : this(controllerId, kingdomName, cultureId, null, null)
    {
    }

    public NetworkRequestCreateKingdom(
        string controllerId,
        string kingdomName,
        string cultureId,
        string partyId,
        string settlementId)
    {
        ControllerId = controllerId;
        KingdomName = kingdomName;
        CultureId = cultureId;
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
