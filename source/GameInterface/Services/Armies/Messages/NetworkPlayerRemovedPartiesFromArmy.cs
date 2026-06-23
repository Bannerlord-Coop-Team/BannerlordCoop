using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to remove parties from an army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerRemovedPartiesFromArmy : ICommand
{
    [ProtoMember(1)]
    public readonly List<string> PartyIds;

    public NetworkPlayerRemovedPartiesFromArmy(List<string> partyIds)
    {
        PartyIds = partyIds;
    }
}