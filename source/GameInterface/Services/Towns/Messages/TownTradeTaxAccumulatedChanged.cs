using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the TradeTaxAccumulated changes in a Town.
    /// </summary>
    public record TownTradeTaxAccumulatedChanged : ICommand
    {
        public string TownId { get; }

        public int TradeTaxAccumulated { get; }

        public TownTradeTaxAccumulatedChanged(string townId, int tradeTaxAccumulated)
        {
            TownId = townId;
            TradeTaxAccumulated = tradeTaxAccumulated;
        }
    }
}
