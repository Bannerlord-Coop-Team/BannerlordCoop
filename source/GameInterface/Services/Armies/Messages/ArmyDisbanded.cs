﻿using Common.Messaging;
using GameInterface.Services.Armies.Data;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when an Army is destroyed
/// </summary>
public record ArmyDisbanded : IEvent
{
    public ArmyDeletionData Data { get; }

    public ArmyDisbanded(ArmyDeletionData armyDeletionData)
    {
        Data = armyDeletionData;
    }

}