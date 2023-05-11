using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

public readonly struct Connect : ICommand
{
    public Guid TransactionID { get; }

    public Connect(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}

public readonly struct Connected : IResponse
{
    public Guid TransactionID { get; }

    public Connected(Guid transactionID, bool clientPartyExists)
    {
        TransactionID = transactionID;
        ClientPartyExists = clientPartyExists;
    }

    public bool ClientPartyExists { get; }


}
