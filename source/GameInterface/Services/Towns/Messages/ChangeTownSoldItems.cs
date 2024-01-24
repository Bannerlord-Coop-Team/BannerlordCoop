using Common.Messaging;
using GameInterface.Services.Towns.ProtoSerializers;
using System.Collections.Generic;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the SoldItems changes in a Town.
    /// </summary>
    public record ChangeTownSoldItems : ICommand
    {
        public string TownId { get; }
        public List<SellLogSerializer> LogList { get; }

        public ChangeTownSoldItems(string townId, List<SellLogSerializer> logList)
        {
            TownId = townId;
            LogList = logList;
        }
    }
}
