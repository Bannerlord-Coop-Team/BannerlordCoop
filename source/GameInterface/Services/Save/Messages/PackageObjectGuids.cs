using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using System;

namespace GameInterface.Services.Heroes.Messages;

public readonly struct PackageObjectGuids : ICommand
{
    public Guid TransactionID { get; }

    public PackageObjectGuids(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}

public readonly struct ObjectGuidsPackaged : IResponse
{
    public Guid TransactionID { get; }
    public string UniqueGameId { get; }
    public GameObjectGuids GameObjectGuids { get; }

    public ObjectGuidsPackaged(
        Guid transactionID,
        string uniqueGameId,
        GameObjectGuids gameObjectGuids)
    {
        TransactionID = transactionID;
        UniqueGameId = uniqueGameId;
        GameObjectGuids = gameObjectGuids;
    }
}
