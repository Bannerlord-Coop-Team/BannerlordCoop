using Common.Messaging;
using GameInterface.Services.Save.Data;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Save.Messages
{
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
}
