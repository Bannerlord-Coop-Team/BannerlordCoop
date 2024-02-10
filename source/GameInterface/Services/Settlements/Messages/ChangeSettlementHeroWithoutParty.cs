using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// change herowithoutparty
/// </summary>
[BatchLogMessage]
public record ChangeSettlementHeroWithoutParty : ICommand
{
    public string SettlementId { get; }
    public string HeroId { get; }

    public ChangeSettlementHeroWithoutParty(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
