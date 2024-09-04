using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Used to let the gameinterface know to change the tradebound village
/// </summary>
public record ChangeVillageTradeBound : ICommand
{
    public string VillageId { get; }
    public string TradeBoundID { get; }

    public ChangeVillageTradeBound(string villageId, string tradeBoundID)
    {
        VillageId = villageId;
        TradeBoundID = tradeBoundID;
    }

}
