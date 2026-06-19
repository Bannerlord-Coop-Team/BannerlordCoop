using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkPlayerKingdomCreated : ICommand
{
    [ProtoMember(1)]
    public string ControllerId { get; }
    [ProtoMember(2)]
    public string KingdomId { get; }
    [ProtoMember(3)]
    public string KingdomName { get; }
    [ProtoMember(4)]
    public string ClanId { get; }
    [ProtoMember(5)]
    public string PartyId { get; }
    [ProtoMember(6)]
    public string SettlementId { get; }

    public NetworkPlayerKingdomCreated(string controllerId, string kingdomId, string kingdomName, string clanId)
        : this(controllerId, kingdomId, kingdomName, clanId, null, null)
    {
    }

    public NetworkPlayerKingdomCreated(
        string controllerId,
        string kingdomId,
        string kingdomName,
        string clanId,
        string partyId,
        string settlementId)
    {
        ControllerId = controllerId;
        KingdomId = kingdomId;
        KingdomName = kingdomName;
        ClanId = clanId;
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
