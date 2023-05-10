using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct EnableGameTimeControls : ICommand
{
    public Guid TransactionID { get; }

    public EnableGameTimeControls(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}
