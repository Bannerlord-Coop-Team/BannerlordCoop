using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkApplyTroopRosterOrder : ICommand
{
    [ProtoMember(1)]
    public readonly uint TroopRosterId;

    [ProtoMember(2)]
    public readonly Dictionary<int, uint> IndexCharacterIds;

    public NetworkApplyTroopRosterOrder(uint troopRosterId, Dictionary<int, uint> indexCharacterIds)
    {
        TroopRosterId = troopRosterId;
        IndexCharacterIds = indexCharacterIds;
    }
}
