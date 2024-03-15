using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Heroes.Messages;

/// <summary>
/// Message from Server to Client notify of new child added.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkNewChildrenAdded :IEvent
{
    [ProtoMember(1)]
    public string HeroId { get; }

    [ProtoMember(2)]
    public string ChildId { get; }

    public NetworkNewChildrenAdded(string heroId, string childId)
    {
        HeroId = heroId;
        ChildId = childId;
    }
}
