using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using System;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct SetTimeControlMode : ICommand
{
    public TimeControlEnum NewTimeMode { get; }
    public Guid TransactionID { get; }

    public SetTimeControlMode(Guid transactionID, TimeControlEnum newTimeMode)
    {
        TransactionID = transactionID;
        NewTimeMode = newTimeMode;
    }
}

public readonly struct TimeControlModeSet : IResponse
{
    public TimeControlEnum NewTimeMode { get; }
    public Guid TransactionID { get; }

    public TimeControlModeSet(Guid transactionID, TimeControlEnum newTimeMode)
    {
        TransactionID = transactionID;
        NewTimeMode = newTimeMode;
    }
}
