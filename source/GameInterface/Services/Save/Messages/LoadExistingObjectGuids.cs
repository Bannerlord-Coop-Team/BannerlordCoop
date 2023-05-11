using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using System;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct LoadExistingObjectGuids : ICommand
{
    public Guid TransactionID { get; }
    public GameObjectGuids GameObjectGuids { get; }

    public LoadExistingObjectGuids(
        Guid transactionID,
        GameObjectGuids gameObjectGuids)
    {
        TransactionID = transactionID;
        GameObjectGuids = gameObjectGuids;
    }
}

public readonly struct ExistingObjectGuidsLoaded : IResponse
{
    public Guid TransactionID { get; }

    public ExistingObjectGuidsLoaded(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}
