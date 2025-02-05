using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the TradeTaxAccumulated changes in a Town.
    /// </summary>
    [BatchLogMessage]
    public record ChangeTownTradeTaxAccumulated : ICommand
    {
        public string TownId { get; }

        public int TradeTaxAccumulated { get; }

        public ChangeTownTradeTaxAccumulated(string townId, int tradeTaxAccumulated)
        {
            TownId = townId;
            TradeTaxAccumulated = tradeTaxAccumulated;
        }
    }
}
