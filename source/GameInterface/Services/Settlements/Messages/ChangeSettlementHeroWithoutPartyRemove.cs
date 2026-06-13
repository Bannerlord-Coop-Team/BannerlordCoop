using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Remove A Hero in Settlement HeroWithoutParty cache
/// </summary>
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
