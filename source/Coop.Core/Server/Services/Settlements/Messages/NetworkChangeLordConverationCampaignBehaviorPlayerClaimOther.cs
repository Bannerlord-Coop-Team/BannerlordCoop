using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Settlements.Messages;

[BatchLogMessage]
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeLordConverationCampaignBehaviorPlayerClaimOther : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkChangeLordConverationCampaignBehaviorPlayerClaimOther(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
