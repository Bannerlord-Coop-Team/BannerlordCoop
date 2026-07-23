using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notifies clients to change Settlement.CanBeClaimed value SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged();
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementClaimantCanBeClaimed : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public int CanBeClaimed { get; }

    public NetworkChangeSettlementClaimantCanBeClaimed(string settlementId, int canBeClaimed)
    {
        SettlementId = settlementId;
        CanBeClaimed = canBeClaimed;
    }
}
