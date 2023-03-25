using Common.Messaging;
using System;

namespace GameInterface.Services.MobileParties.Messages
{
    public readonly struct PartiesRegistered : IResponse
    {
        public Guid TransactionID { get; }

        public PartiesRegistered(Guid transactionId)
        {
            TransactionID = transactionId;
        }
    }
}