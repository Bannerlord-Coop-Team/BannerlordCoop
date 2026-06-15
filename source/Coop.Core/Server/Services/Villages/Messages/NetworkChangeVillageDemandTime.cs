using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Villages.Messages;

/// <summary>
/// Notify client of LastDemandTimeSatifised Change.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeVillageDemandTime : IEvent
{
    [ProtoMember(1)]
    public string VillageId { get; }
    [ProtoMember(2)]
    public float LastDemandSatisifedTime { get; }

    public NetworkChangeVillageDemandTime(string villageId, float lastDemandSatisifedTime)
    {
        VillageId = villageId;
        LastDemandSatisifedTime = lastDemandSatisifedTime;
    }
}
