using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkRequestCreateKingdom : ICommand
{
    [ProtoMember(1)]
    public string KingdomName { get; }
    [ProtoMember(2)]
    public string CultureId { get; }
    [ProtoMember(3)]
    public string PartyId { get; }
    [ProtoMember(4)]
    public string SettlementId { get; }

    public NetworkRequestCreateKingdom(string kingdomName, string cultureId)
        : this(kingdomName, cultureId, null, null)
    {
    }

    public NetworkRequestCreateKingdom(
        string kingdomName,
        string cultureId,
        string partyId,
        string settlementId)
    {
        KingdomName = kingdomName;
        CultureId = cultureId;
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
