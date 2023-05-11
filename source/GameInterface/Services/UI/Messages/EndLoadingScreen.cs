using Common.Messaging;
using System;

namespace GameInterface.Services.UI.Messages;

public readonly struct EndLoadingScreen : ICommand
{
    public Guid TransactionID { get; }

    public EndLoadingScreen(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}
