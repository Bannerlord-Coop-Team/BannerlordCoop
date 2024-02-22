using Common.Logging.Attributes;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Settlement HeroWithoutParty was removed notify server.
/// </summary>
[BatchLogMessage]
public record SettlementChangedRemoveHeroWithoutParty : IEvent
{
    public string SettlementId { get; }
    public string HeroId { get; }

    public SettlementChangedRemoveHeroWithoutParty(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
