using Common.Messaging;
using GameInterface.Services.Towns.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages;

/// <summary>
/// Used when the sold items change in a town.
/// </summary>
public readonly struct TownSoldItemsChanged : ICommand
{
    public readonly Town Town;
    public readonly SellLogData[] LogList;

    public TownSoldItemsChanged(Town town, IEnumerable<Town.SellLog> logList)
    {
        Town = town;
        LogList = logList
            .Select(sellLog => new SellLogData(sellLog.Number, sellLog.Category.StringId))
            .ToArray();
    }
}