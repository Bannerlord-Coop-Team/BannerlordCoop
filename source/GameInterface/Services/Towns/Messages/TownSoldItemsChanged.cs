using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the SoldItems changes in a Town.
    /// </summary>
    public record TownSoldItemsChanged : ICommand
    {
        public string TownId { get; }
        public IEnumerable<Town.SellLog> LogList { get; }

        public TownSoldItemsChanged(string townId, IEnumerable<Town.SellLog> logList)
        {
            TownId = townId;
            LogList = logList;
        }
    }
}
