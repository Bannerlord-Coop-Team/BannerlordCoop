using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Settlements.Messages;

/// <summary>
/// Client notifies server of claim request
/// </summary>
[BatchLogMessage]
[ProtoContract(SkipConstructor = true)] 
public record ClientChangeLordConversationCampaignBehaviorPlayerClaim : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public ClientChangeLordConversationCampaignBehaviorPlayerClaim(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
