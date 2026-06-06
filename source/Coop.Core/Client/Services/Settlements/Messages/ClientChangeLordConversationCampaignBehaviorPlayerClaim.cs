using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Settlements.Messages;

/// <summary>
/// Client notifies server of claim request
/// </summary>
[BatchLogMessage]
[ProtoContract(SkipConstructor = true)] 
public readonly struct ClientChangeLordConversationCampaignBehaviorPlayerClaim : IEvent
{
    [ProtoMember(1)]
    public readonly string SettlementId;
    [ProtoMember(2)]
    public readonly string HeroId;

    public ClientChangeLordConversationCampaignBehaviorPlayerClaim(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
