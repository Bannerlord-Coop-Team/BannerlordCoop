using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When a hero is attached to hero cache
/// </summary>
[BatchLogMessage]
public record SettlementChangedAddHeroWithoutParty : IEvent
{
    public string SettlementId { get; }
    public string HeroId {  get; }

    public SettlementChangedAddHeroWithoutParty(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
