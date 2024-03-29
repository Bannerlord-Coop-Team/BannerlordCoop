﻿using Common.Messaging;
using GameInterface.Services.Towns.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the SoldItems changes in a Town.
    /// </summary>
    public record TownSoldItemsChanged : ICommand
    {
        public string TownId { get; }
        public SellLogData[] LogList { get; }

        public TownSoldItemsChanged(string townId, IEnumerable<Town.SellLog> logList)
        {
            TownId = townId;
            LogList = logList.Select(sellLog => new SellLogData(sellLog.Number, sellLog.Category.StringId)).ToArray();
        }
    }
}
