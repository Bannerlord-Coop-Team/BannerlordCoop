using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct HeroesRegistered : IEvent
    {
        public Guid TransactionID { get; }

        public HeroesRegistered(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}
