using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add parties to an existing army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerAddedPartiesToArmy : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly List<string> PartyIds;

    public NetworkPlayerAddedPartiesToArmy(string armyId, List<string> partyIds)
    {
        ArmyId = armyId;
        PartyIds = partyIds;
    }
}