using Common.Logging.Attributes;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages;

[BatchLogMessage]
public record ChangeSettlementHeroWithoutPartyRemove : ICommand
{
    public string SettlementId { get; }
    public string HeroId { get; }

    public ChangeSettlementHeroWithoutPartyRemove(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
