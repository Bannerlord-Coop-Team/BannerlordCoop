using Common.Logging.Attributes;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// Message used when LastDemandTimeSatifisfied Changes.
/// </summary>
/// 
[BatchLogMessage]
public record VillageDemandTimeChanged : ICommand
{
    public string VillageId { get; }
    public float LastDemandSatisfiedTime { get; }

    public VillageDemandTimeChanged(string villageId, float lastDemandSatisfiedTime)
    {
        VillageId = villageId;
        LastDemandSatisfiedTime = lastDemandSatisfiedTime;
    }
}
