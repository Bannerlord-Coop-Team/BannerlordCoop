using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct ResolveHeroNotFound : IResponse
{
    public Guid TransactionID { get; }

    public ResolveHeroNotFound(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}