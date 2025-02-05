using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _homeSettlement
    /// </summary>
    public record HomeSettlementChanged : IEvent
    {
        public string SettlementStringId { get; }
        public string HeroId { get; }

        public HomeSettlementChanged(string settlementStringId, string heroId)
        {
            SettlementStringId = settlementStringId;
            HeroId = heroId;
        }
    }
}