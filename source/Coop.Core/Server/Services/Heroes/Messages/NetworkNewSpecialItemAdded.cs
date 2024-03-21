using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.Heroes.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkNewSpecialItemAdded : IEvent
{
    [ProtoMember(1)]
    public string HeroId { get; }
    [ProtoMember(2)]
    public string ItemObjectId { get; }

    public NetworkNewSpecialItemAdded(string heroId, string specialItem)
    {
        HeroId = heroId;
        ItemObjectId = specialItem;
    }
}
