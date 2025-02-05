using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;
/// <summary>
/// Client publish for _homeSettlement of Hero
/// </summary>
public record ChangeHomeSettlement : ICommand
{
    public string SettlementStringId { get; }
    public string HeroId { get; }

    public ChangeHomeSettlement(string settlementStringId, string heroId)
    {
        SettlementStringId = settlementStringId;
        HeroId = heroId;
    }
}