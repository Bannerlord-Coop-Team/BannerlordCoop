using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using Common.Messaging;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add a to set AiBehaviorObject on army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSetArmyAiBehaviorObject : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly string AiBehaviorObjectId;
    [ProtoMember(3)]
    public readonly bool IsSettlement;

    public NetworkSetArmyAiBehaviorObject(string armyId, string aiBehaviorObjectId, bool isSettlement)
    {
        ArmyId = armyId;
        AiBehaviorObjectId = aiBehaviorObjectId;
        IsSettlement = isSettlement;
    }
}