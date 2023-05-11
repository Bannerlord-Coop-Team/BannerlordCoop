using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct RegisterAllGameObjects : ICommand
{
    public Guid TransactionID { get; }

    public RegisterAllGameObjects(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}

public readonly struct AllGameObjectsRegistered : IResponse
{
    public Guid TransactionID { get; }

    public AllGameObjectsRegistered(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}