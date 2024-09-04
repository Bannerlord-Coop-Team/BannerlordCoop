using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.Towns.Data;
using System.Collections.Generic;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the SoldItems changes in a Town.
    /// </summary>
    [BatchLogMessage]
    public record ChangeTownSoldItems : ICommand
    {
        public string TownId { get; }
        public SellLogData[] LogList { get; }

        public ChangeTownSoldItems(string townId, SellLogData[] logList)
        {
            TownId = townId;
            LogList = logList;
        }
    }
}
