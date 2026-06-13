using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Villages.Messages;

/// <summary>
/// Server sends this data when a Village Hearth's value changes.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeVillageHearth : IEvent
{
    [ProtoMember(1)]
    public string VillageId { get; }
    [ProtoMember(2)]
    public float Hearth { get; }

    public NetworkChangeVillageHearth(string villageId, float hearth)
    {
        VillageId = villageId;
        Hearth = hearth;
    }

}
