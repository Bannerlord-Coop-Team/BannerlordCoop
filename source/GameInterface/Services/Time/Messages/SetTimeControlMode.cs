using Common.Messaging;
using GameInterface.Services.Time.Enum;
using System;

namespace GameInterface.Services.Time.Messages
{
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
}
