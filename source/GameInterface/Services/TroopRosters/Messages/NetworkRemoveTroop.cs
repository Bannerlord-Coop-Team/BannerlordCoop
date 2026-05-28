using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRemoveTroop : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;
    [ProtoMember(2)]
    public readonly string TroopId;
    [ProtoMember(3)]
    public readonly int NumberToRemove;
    [ProtoMember(4)]
    public readonly int Xp;

    public NetworkRemoveTroop(string troopRosterId, string troopId, int numberToRemove, int xp)
    {
        TroopRosterId = troopRosterId;
        TroopId = troopId;
        NumberToRemove = numberToRemove;
        Xp = xp;
    }
}
