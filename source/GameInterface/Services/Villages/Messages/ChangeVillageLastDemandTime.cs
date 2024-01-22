using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Message to gameinterface for changing village last demand satifisied time
/// </summary>
public record ChangeVillageLastDemandTime : ICommand
{
    public string VillageId { get; }
    public float LastDemandSatifiedTime { get; }

    public ChangeVillageLastDemandTime(string villageId, float lastDemandSatifiedTime)
    {
        VillageId = villageId;
        LastDemandSatifiedTime = lastDemandSatifiedTime;
    }
}
