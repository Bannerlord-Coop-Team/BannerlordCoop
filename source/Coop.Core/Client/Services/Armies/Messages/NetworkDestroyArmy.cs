﻿using Common.Messaging;
using GameInterface.Services.Armies.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Armies.Messages;

/// <summary>
/// Network message to commmand the deletion of an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkDestroyArmy : ICommand
{
    [ProtoMember(1)]
    public ArmyDestructionData Data { get; }

    public NetworkDestroyArmy(ArmyDestructionData armyDeletionData)
    {
        Data = armyDeletionData;
    }
}