using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Settlements.Messages;

/// <summary>
/// Notify Server that client is attempting to change Settlement.ClaimValue
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record ClientChangeLordConversationCampaignBehaviorPlayerClaimValue : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public float ClaimValue { get; }

    public ClientChangeLordConversationCampaignBehaviorPlayerClaimValue(string settlementId, float claimValue)
    {
        SettlementId = settlementId;
        ClaimValue = claimValue;
    }
}
