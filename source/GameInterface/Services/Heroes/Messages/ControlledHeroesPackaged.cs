using Common.Messaging;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct ControlledHeroesPackaged : IResponse
    {
        public Guid TransactionID { get; }
        public HashSet<Guid> ControlledHeros { get; }

        public ControlledHeroesPackaged(Guid transactionID, HashSet<Guid> controlledHeros)
        {
            TransactionID = transactionID;
            ControlledHeros = controlledHeros;
        }
    }
}
