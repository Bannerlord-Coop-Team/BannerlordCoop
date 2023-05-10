using Common.Messaging;
using System;

namespace GameInterface.Services.MobileParties.Messages;

public readonly struct RemoveMainParty : ICommand
{
    public Guid TransactionID { get; }

    public RemoveMainParty(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}

public readonly struct MainPartyRemoved : IResponse
{
    public Guid TransactionID { get; }

    public MainPartyRemoved(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}
